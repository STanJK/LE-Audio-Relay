using System.IO.Pipes;
using LEAudioRouter.Supervision;

namespace LEAudioRouter.Host;

internal static class BackendWorkerPlaceholder
{
    public static int Run(string[] args)
    {
        try
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }
        catch
        {
            // A backend generation is disposable by design. Never surface an
            // unhandled worker exception into the shell process.
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        string pipeName = GetRequired(args, "--pipe");
        long generation = GetRequiredLong(args, "--generation");
        string mode = GetRequired(args, "--mode");
        string destination = GetRequired(args, "--dest");

        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var connectTimeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        await pipe.ConnectAsync(connectTimeout.Token);

        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe)
        {
            AutoFlush = true
        };

        await SendAsync(
            writer,
            new WorkerEnvelope(
                WorkerProtocol.Hello,
                generation,
                $"mode={mode}; dest={destination}"));

        await SendAsync(
            writer,
            new WorkerEnvelope(
                WorkerProtocol.Running,
                generation));

        using var heartbeatCts = new CancellationTokenSource();

        Task heartbeatTask = Task.Run(
            () => HeartbeatLoopAsync(
                writer,
                generation,
                heartbeatCts.Token));

        try
        {
            while (true)
            {
                string? line = await reader.ReadLineAsync();

                if (line is null)
                {
                    return 0;
                }

                WorkerEnvelope command =
                    WorkerProtocol.Deserialize(line);

                if (command.Generation != generation)
                {
                    continue;
                }

                if (command.Type == WorkerProtocol.Shutdown)
                {
                    await SendAsync(
                        writer,
                        new WorkerEnvelope(
                            WorkerProtocol.Stopping,
                            generation));

                    return 0;
                }
            }
        }
        finally
        {
            heartbeatCts.Cancel();

            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await SendAsync(
                    writer,
                    new WorkerEnvelope(
                        WorkerProtocol.Stopped,
                        generation));
            }
            catch
            {
            }
        }
    }

    private static async Task HeartbeatLoopAsync(
        StreamWriter writer,
        long generation,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);

            await SendAsync(
                writer,
                new WorkerEnvelope(
                    WorkerProtocol.Heartbeat,
                    generation));
        }
    }

    private static Task SendAsync(
        StreamWriter writer,
        WorkerEnvelope message) =>
        writer.WriteLineAsync(
            WorkerProtocol.Serialize(message));

    private static string GetRequired(
        string[] args,
        string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
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
        string raw = GetRequired(args, name);

        return long.TryParse(raw, out long value)
            ? value
            : throw new ArgumentException(
                $"Invalid worker argument {name}: {raw}.");
    }
}
