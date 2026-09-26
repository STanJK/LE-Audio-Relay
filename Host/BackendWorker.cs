using System.IO.Pipes;
using LEAudioRouter.Routing;
using LEAudioRouter.Settings;
using LEAudioRouter.Supervision;

namespace LEAudioRouter.Host;

internal static class BackendWorker
{
    public static int Run(
        string[] args)
    {
        try
        {
            return RunAsync(args)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return 1;
        }
    }

    private static async Task<int> RunAsync(
        string[] args)
    {
        string pipeName =
            GetRequired(
                args,
                "--pipe");

        long generation =
            GetRequiredLong(
                args,
                "--generation");

        RouterMode mode =
            GetRequiredMode(
                args,
                "--mode");

        string destination =
            GetRequired(
                args,
                "--dest");

        var configuration =
            new RouteGenerationConfiguration(
                mode,
                destination);

        using var pipe =
            new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

        using var connectTimeout =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

        await pipe.ConnectAsync(
            connectTimeout.Token);

        using var reader =
            new StreamReader(pipe);

        using var writer =
            new StreamWriter(pipe)
            {
                AutoFlush = true
            };

        using var sendGate =
            new SemaphoreSlim(
                1,
                1);

        using var lifetimeCts =
            new CancellationTokenSource();

        RouteSession? route = null;
        Task? heartbeatTask = null;

        Task commandTask =
            WatchCommandsAsync(
                reader,
                generation,
                lifetimeCts);

        try
        {
            await SendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Hello,
                    generation,
                    $"mode={mode}; dest={destination}"));

            Task<RouteSession> routeStartTask =
                RouteSession.StartAsync(
                    configuration,
                    lifetimeCts.Token);

            Task first =
                await Task.WhenAny(
                    routeStartTask,
                    commandTask);

            if (first == commandTask)
            {
                lifetimeCts.Cancel();

                try
                {
                    route =
                        await routeStartTask;
                }
                catch
                {
                }

                return 0;
            }

            route =
                await routeStartTask;

            await SendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Running,
                    generation));

            heartbeatTask =
                HeartbeatLoopAsync(
                    writer,
                    sendGate,
                    generation,
                    route,
                    lifetimeCts.Token);

            Task completed =
                await Task.WhenAny(
                    commandTask,
                    route.Failure);

            if (completed == route.Failure)
            {
                RouteFailure failure =
                    await route.Failure;

                await SafeSendAsync(
                    writer,
                    sendGate,
                    new WorkerEnvelope(
                        WorkerProtocol.Faulted,
                        generation,
                        failure.ToString()));

                return 1;
            }

            await SafeSendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Stopping,
                    generation));

            return 0;
        }
        catch (OperationCanceledException)
            when (lifetimeCts.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            await SafeSendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Faulted,
                    generation,
                    ex.Message));

            return 1;
        }
        finally
        {
            lifetimeCts.Cancel();

            if (heartbeatTask is not null)
            {
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                }
            }

            if (route is not null)
            {
                try
                {
                    await route.DisposeAsync();
                }
                catch
                {
                }
            }

            await SafeSendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Stopped,
                    generation));
        }
    }

    private static async Task WatchCommandsAsync(
        StreamReader reader,
        long generation,
        CancellationTokenSource lifetimeCts)
    {
        try
        {
            while (!lifetimeCts.IsCancellationRequested)
            {
                string? line =
                    await reader.ReadLineAsync(
                        lifetimeCts.Token);

                if (line is null)
                {
                    lifetimeCts.Cancel();
                    return;
                }

                WorkerEnvelope command =
                    WorkerProtocol.Deserialize(
                        line);

                if (command.Generation != generation)
                {
                    continue;
                }

                if (command.Type ==
                    WorkerProtocol.Shutdown)
                {
                    lifetimeCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            lifetimeCts.Cancel();
        }
    }

    private static async Task HeartbeatLoopAsync(
        StreamWriter writer,
        SemaphoreSlim sendGate,
        long generation,
        RouteSession route,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);

            RouteTelemetrySnapshot snapshot =
                route.Telemetry;

            string detail =
                $"cap={snapshot.CapturePackets}; " +
                $"dropA={snapshot.RuntimeDroppedAudioFrames}; " +
                $"dropS={snapshot.RuntimeDroppedSilentFrames}; " +
                $"zero={snapshot.RenderZeroFillFrames}";

            await SendAsync(
                writer,
                sendGate,
                new WorkerEnvelope(
                    WorkerProtocol.Heartbeat,
                    generation,
                    detail));
        }
    }

    private static async Task SendAsync(
        StreamWriter writer,
        SemaphoreSlim sendGate,
        WorkerEnvelope message)
    {
        await sendGate.WaitAsync();

        try
        {
            await writer.WriteLineAsync(
                WorkerProtocol.Serialize(
                    message));
        }
        finally
        {
            sendGate.Release();
        }
    }

    private static async Task SafeSendAsync(
        StreamWriter writer,
        SemaphoreSlim sendGate,
        WorkerEnvelope message)
    {
        try
        {
            await SendAsync(
                writer,
                sendGate,
                message);
        }
        catch
        {
        }
    }

    private static string GetRequired(
        string[] args,
        string name)
    {
        for (int i = 0;
             i < args.Length - 1;
             i++)
        {
            if (args[i].Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        throw new ArgumentException(
            $"Missing required worker argument {name}.");
    }

    private static long GetRequiredLong(
        string[] args,
        string name)
    {
        string raw =
            GetRequired(
                args,
                name);

        return long.TryParse(
                raw,
                out long value)
            ? value
            : throw new ArgumentException(
                $"Invalid worker argument {name}: {raw}.");
    }

    private static RouterMode GetRequiredMode(
        string[] args,
        string name)
    {
        string raw =
            GetRequired(
                args,
                name);

        return Enum.TryParse(
                raw,
                ignoreCase: true,
                out RouterMode mode)
            ? mode
            : throw new ArgumentException(
                $"Invalid worker router mode: {raw}.");
    }
}
