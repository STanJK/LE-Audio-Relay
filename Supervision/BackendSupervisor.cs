using System.Diagnostics;
using System.IO.Pipes;
using LEAudioRouter.Settings;

namespace LEAudioRouter.Supervision;

internal sealed class BackendSupervisor : IDisposable
{
    private static readonly TimeSpan ReconcilePeriod =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan WorkerHandshakeTimeout =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan WorkerHeartbeatTimeout =
        TimeSpan.FromSeconds(4);

    private static readonly TimeSpan WorkerShutdownTimeout =
        TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly RouterConfiguration _configuration;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private SupervisorState _state =
        SupervisorState.Idle;

    private Task? _loopTask;

    private long _restartRevision;
    private long _nextBackendGeneration;
    private long? _activeBackendGeneration;
    private string? _lastError;

    public BackendSupervisor(
        RouterConfiguration configuration)
    {
        _configuration = configuration;
    }

    public event EventHandler? StateChanged;

    public SupervisorState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public long? ActiveBackendGeneration
    {
        get
        {
            lock (_gate)
            {
                return _activeBackendGeneration;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_gate)
            {
                return _lastError;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_loopTask is not null)
            {
                return;
            }

            _loopTask = Task.Run(
                () => ReconcileLoopAsync(
                    _lifetimeCts.Token));
        }

        PublishState(
            SupervisorState.Starting);
    }

    public void RequestRestart()
    {
        lock (_gate)
        {
            _restartRevision++;
        }

        PublishState(
            SupervisorState.RestartRequested);
    }

    public void SetMode(
        RouterMode mode)
    {
        bool changed;

        lock (_gate)
        {
            changed =
                _configuration.Mode != mode;

            if (changed)
            {
                _configuration.Mode = mode;
                _restartRevision++;
            }
        }

        if (changed)
        {
            PublishState(
                SupervisorState.RestartRequested);
        }
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();

        Task? loop;

        lock (_gate)
        {
            loop = _loopTask;
        }

        if (loop is not null)
        {
            try
            {
                loop.Wait(
                    TimeSpan.FromSeconds(4));
            }
            catch
            {
            }
        }

        _lifetimeCts.Dispose();

        PublishState(
            SupervisorState.Stopped);
    }

    private async Task ReconcileLoopAsync(
        CancellationToken cancellationToken)
    {
        WorkerGeneration? active = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConfigurationSnapshot desired =
                    SnapshotConfiguration();

                if (active is not null &&
                    active.Completion.IsCompleted)
                {
                    string? failure =
                        await ReadCompletionFailureAsync(
                            active.Completion);

                    await active.DisposeAsync();

                    active = null;
                    SetActiveGeneration(null);

                    if (failure is not null)
                    {
                        SetLastError(failure);
                        PublishState(
                            SupervisorState.Faulted);
                    }

                    await DelayAsync(
                        cancellationToken);

                    continue;
                }

                bool generationIsStale =
                    active is not null &&
                    (active.RestartRevision !=
                        desired.RestartRevision ||
                     active.Configuration !=
                        desired.Configuration);

                if (generationIsStale)
                {
                    PublishState(
                        SupervisorState.Restarting);

                    await StopGenerationAsync(
                        active!,
                        cancellationToken);

                    active = null;
                    SetActiveGeneration(null);
                }

                if (active is null)
                {
                    PublishState(
                        SupervisorState.Starting);

                    try
                    {
                        active =
                            await StartGenerationAsync(
                                desired,
                                cancellationToken);

                        SetActiveGeneration(
                            active.Generation);

                        SetLastError(null);

                        PublishState(
                            SupervisorState.Running);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        SetLastError(ex.Message);

                        PublishState(
                            SupervisorState.Faulted);

                        await DelayAsync(
                            cancellationToken);

                        continue;
                    }
                }

                await DelayAsync(
                    cancellationToken);
            }
        }
        finally
        {
            if (active is not null)
            {
                try
                {
                    await StopGenerationAsync(
                        active,
                        CancellationToken.None);
                }
                catch
                {
                }

                SetActiveGeneration(null);
            }
        }
    }

    private async Task<WorkerGeneration> StartGenerationAsync(
        ConfigurationSnapshot desired,
        CancellationToken cancellationToken)
    {
        long generation =
            Interlocked.Increment(
                ref _nextBackendGeneration);

        string pipeName =
            $"LEAudioRouter.Backend." +
            $"{Environment.ProcessId}." +
            $"{generation}." +
            $"{Guid.NewGuid():N}";

        var pipe =
            new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

        Process? process = null;

        try
        {
            process =
                StartWorkerProcess(
                    pipeName,
                    generation,
                    desired.Configuration);

            using var handshakeCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            handshakeCts.CancelAfter(
                WorkerHandshakeTimeout);

            await pipe.WaitForConnectionAsync(
                handshakeCts.Token);

            var reader =
                new StreamReader(pipe);

            var writer =
                new StreamWriter(pipe)
                {
                    AutoFlush = true
                };

            WorkerEnvelope hello =
                await ReadMessageWithTimeoutAsync(
                    reader,
                    WorkerHandshakeTimeout,
                    cancellationToken);

            if (hello.Type != WorkerProtocol.Hello ||
                hello.Generation != generation)
            {
                throw new InvalidDataException(
                    "Backend worker returned an invalid handshake.");
            }

            Task<string?> completion =
                MonitorWorkerAsync(
                    reader,
                    process,
                    generation,
                    cancellationToken);

            return new WorkerGeneration(
                generation,
                desired.RestartRevision,
                desired.Configuration,
                process,
                pipe,
                reader,
                writer,
                completion);
        }
        catch
        {
            if (process is not null)
            {
                TryKill(process);
                process.Dispose();
            }

            pipe.Dispose();
            throw;
        }
    }

    private static Process StartWorkerProcess(
        string pipeName,
        long generation,
        RouteGenerationConfiguration configuration)
    {
        string? processPath =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(
                processPath))
        {
            throw new InvalidOperationException(
                "Environment.ProcessPath is unavailable.");
        }

        string[] currentArgs =
            Environment.GetCommandLineArgs();

        var info =
            new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

        bool launchedByDotnet =
            Path.GetFileNameWithoutExtension(
                    processPath)
                .Equals(
                    "dotnet",
                    StringComparison.OrdinalIgnoreCase);

        if (launchedByDotnet)
        {
            info.FileName = processPath;

            if (currentArgs.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine current assembly path.");
            }

            info.ArgumentList.Add(
                currentArgs[0]);
        }
        else
        {
            info.FileName = processPath;
        }

        info.ArgumentList.Add(
            "--backend-worker");

        info.ArgumentList.Add(
            "--pipe");

        info.ArgumentList.Add(
            pipeName);

        info.ArgumentList.Add(
            "--generation");

        info.ArgumentList.Add(
            generation.ToString());

        info.ArgumentList.Add(
            "--mode");

        info.ArgumentList.Add(
            configuration.Mode.ToString());

        info.ArgumentList.Add(
            "--dest");

        info.ArgumentList.Add(
            configuration.DestinationMatch);

        return Process.Start(info)
            ?? throw new InvalidOperationException(
                "Failed to start backend worker process.");
    }

    private static async Task<string?> MonitorWorkerAsync(
        StreamReader reader,
        Process process,
        long generation,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                return
                    $"Backend generation {generation} exited " +
                    $"with code {process.ExitCode}.";
            }

            WorkerEnvelope message;

            try
            {
                message =
                    await ReadMessageWithTimeoutAsync(
                        reader,
                        WorkerHeartbeatTimeout,
                        cancellationToken);
            }
            catch (TimeoutException)
            {
                return
                    $"Backend generation {generation} heartbeat timed out.";
            }
            catch (EndOfStreamException)
            {
                return
                    $"Backend generation {generation} closed its control pipe.";
            }

            if (message.Generation != generation)
            {
                continue;
            }

            if (message.Type == WorkerProtocol.Faulted)
            {
                return
                    message.Detail ??
                    $"Backend generation {generation} reported a fault.";
            }

            if (message.Type == WorkerProtocol.Stopped)
            {
                return null;
            }
        }

        return null;
    }

    private static async Task<WorkerEnvelope>
        ReadMessageWithTimeoutAsync(
            StreamReader reader,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        Task<string?> readTask =
            reader.ReadLineAsync(
                    cancellationToken)
                .AsTask();

        string? line =
            await readTask.WaitAsync(
                timeout,
                cancellationToken);

        if (line is null)
        {
            throw new EndOfStreamException();
        }

        return WorkerProtocol.Deserialize(
            line);
    }

    private static async Task StopGenerationAsync(
        WorkerGeneration generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await generation.Writer.WriteLineAsync(
                WorkerProtocol.Serialize(
                    new WorkerEnvelope(
                        WorkerProtocol.Shutdown,
                        generation.Generation)));
        }
        catch
        {
        }

        try
        {
            await generation.Completion.WaitAsync(
                WorkerShutdownTimeout,
                cancellationToken);
        }
        catch
        {
            TryKill(
                generation.Process);
        }

        if (!generation.Process.HasExited)
        {
            TryKill(
                generation.Process);
        }

        await generation.DisposeAsync();
    }

    private static void TryKill(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);

                process.WaitForExit(1000);
            }
        }
        catch
        {
        }
    }

    private static async Task<string?>
        ReadCompletionFailureAsync(
            Task<string?> completion)
    {
        try
        {
            return await completion;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private ConfigurationSnapshot SnapshotConfiguration()
    {
        lock (_gate)
        {
            return new ConfigurationSnapshot(
                _configuration.Snapshot(),
                _restartRevision);
        }
    }

    private void SetActiveGeneration(
        long? generation)
    {
        lock (_gate)
        {
            _activeBackendGeneration =
                generation;
        }

        RaiseStateChanged();
    }

    private void SetLastError(
        string? error)
    {
        lock (_gate)
        {
            _lastError = error;
        }

        RaiseStateChanged();
    }

    private void PublishState(
        SupervisorState state)
    {
        bool changed;

        lock (_gate)
        {
            changed =
                _state != state;

            _state = state;
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    private void RaiseStateChanged() =>
        StateChanged?.Invoke(
            this,
            EventArgs.Empty);

    private static async Task DelayAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                ReconcilePeriod,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private readonly record struct ConfigurationSnapshot(
        RouteGenerationConfiguration Configuration,
        long RestartRevision);

    private sealed class WorkerGeneration :
        IAsyncDisposable
    {
        public WorkerGeneration(
            long generation,
            long restartRevision,
            RouteGenerationConfiguration configuration,
            Process process,
            NamedPipeServerStream pipe,
            StreamReader reader,
            StreamWriter writer,
            Task<string?> completion)
        {
            Generation = generation;
            RestartRevision = restartRevision;
            Configuration = configuration;
            Process = process;
            Pipe = pipe;
            Reader = reader;
            Writer = writer;
            Completion = completion;
        }

        public long Generation { get; }

        public long RestartRevision { get; }

        public RouteGenerationConfiguration Configuration { get; }

        public Process Process { get; }

        public NamedPipeServerStream Pipe { get; }

        public StreamReader Reader { get; }

        public StreamWriter Writer { get; }

        public Task<string?> Completion { get; }

        public ValueTask DisposeAsync()
        {
            Writer.Dispose();
            Reader.Dispose();
            Pipe.Dispose();
            Process.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
