using System.Diagnostics;
using System.IO.Pipes;
using LEAudioRelay.Lifecycle;
using LEAudioRelay.Settings;
using LEAudioRelay.Telemetry;

namespace LEAudioRelay.Supervision;

internal sealed class BackendSupervisor :
    IDisposable
{
    private static readonly TimeSpan
        TopologySafetyProbeInterval =
            TimeSpan.FromSeconds(30);

    private static readonly TimeSpan WorkerHandshakeTimeout =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan WorkerStartupTimeout =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan WorkerHeartbeatTimeout =
        TimeSpan.FromSeconds(4);

    private static readonly TimeSpan WorkerShutdownCommandTimeout =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan WorkerShutdownTimeout =
        TimeSpan.FromSeconds(2);

    private static readonly TimeSpan[] RecoveryBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly object _gate =
        new();

    private readonly RelayConfiguration _configuration;
    private readonly LifecycleEventLog _lifecycleLog;

    private readonly CancellationTokenSource _lifetimeCts =
        new();

    private readonly SemaphoreSlim _wakeSignal =
        new(
            initialCount: 0,
            maxCount: 1);

    private PowerSnapshot _powerSnapshot =
        new(
            IsSuspended: false,
            Revision: 0,
            SuspendCount: 0);

    private SupervisorState _state =
        SupervisorState.Idle;

    private Task? _loopTask;

    private long _restartRevision;
    private long _nextBackendGeneration;
    private long? _activeBackendGeneration;
    private string? _lastError;

    public BackendSupervisor(
        RelayConfiguration configuration,
        LifecycleEventLog lifecycleLog)
    {
        _configuration =
            configuration;

        _lifecycleLog =
            lifecycleLog;
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

    public void UpdatePowerSnapshot(
        PowerSnapshot snapshot)
    {
        lock (_gate)
        {
            _powerSnapshot =
                snapshot;
        }

        Wake();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_loopTask is not null)
            {
                return;
            }

            _loopTask =
                Task.Run(
                    () =>
                        ReconcileLoopAsync(
                            _lifetimeCts.Token));
        }

        PublishState(
            SupervisorState.Starting);

        Wake();
    }

    public void RequestRestart()
    {
        lock (_gate)
        {
            _restartRevision++;
        }

        _lifecycleLog.Write(
            "MANUAL_RESTART");

        PublishState(
            SupervisorState.RestartRequested);

        Wake();
    }

    public void SetMode(
        RelayMode mode)
    {
        bool changed;
        RelayMode previous;

        lock (_gate)
        {
            previous =
                _configuration.Mode;

            changed =
                previous !=
                mode;

            if (changed)
            {
                _configuration.Mode =
                    mode;

                _restartRevision++;
            }
        }

        if (changed)
        {
            _lifecycleLog.Write(
                "MODE_CHANGE",
                $"from={previous}; to={mode}");

            PublishState(
                SupervisorState.RestartRequested);

            Wake();
        }
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        Wake();

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

        _wakeSignal.Dispose();
        _lifetimeCts.Dispose();

        PublishState(
            SupervisorState.Stopped);
    }

    private async Task ReconcileLoopAsync(
        CancellationToken cancellationToken)
    {
        WorkerGeneration? active =
            null;

        AudioEndpointObserver? observer =
            null;

        AudioEndpointProbe? probe =
            null;

        string? pendingWorkerFailure =
            null;

        int consecutiveFailures =
            0;

        PowerSnapshot? lastObservedPower =
            null;

        string? lastEndpointLogKey =
            null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PowerSnapshot power =
                    SnapshotPower();

                LogPowerTransition(
                    power,
                    ref lastObservedPower);

                if (power.IsSuspended)
                {
                    pendingWorkerFailure =
                        null;

                    consecutiveFailures =
                        0;

                    SetLastError(
                        null);

                    PublishState(
                        SupervisorState.Suspended);

                    await WaitForWakeAsync(
                        cancellationToken);

                    continue;
                }

                if (observer is null ||
                    probe is null)
                {
                    try
                    {
                        observer =
                            new AudioEndpointObserver();

                        observer.TopologyChanged +=
                            OnEndpointTopologyChanged;

                        probe =
                            new AudioEndpointProbe();

                        consecutiveFailures =
                            0;

                        SetLastError(
                            null);
                    }
                    catch (Exception ex)
                    {
                        if (observer is not null)
                        {
                            observer.TopologyChanged -=
                                OnEndpointTopologyChanged;

                            observer.Dispose();
                            observer = null;
                        }

                        probe?.Dispose();
                        probe = null;

                        consecutiveFailures++;

                        SetLastError(
                            $"Endpoint observation unavailable: {ex.Message}");

                        PublishState(
                            SupervisorState.RecoveringFault);

                        await WaitForWakeOrDelayAsync(
                            GetRecoveryDelay(
                                consecutiveFailures),
                            cancellationToken);

                        continue;
                    }
                }

                ConfigurationSnapshot desired =
                    SnapshotConfiguration();

                AudioEndpointSnapshot reality;

                try
                {
                    reality =
                        probe.Probe(
                            desired.Configuration.DestinationMatch);

                    LogEndpointTransition(
                        reality,
                        ref lastEndpointLogKey);
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;

                    SetLastError(
                        $"Endpoint probe failed: {ex.Message}");

                    PublishState(
                        SupervisorState.RecoveringFault);

                    if (active is not null)
                    {
                        bool workerCompleted =
                            await WaitForWorkerWakeOrDelayAsync(
                                active,
                                GetRecoveryDelay(
                                    consecutiveFailures),
                                cancellationToken);

                        if (workerCompleted)
                        {
                            pendingWorkerFailure =
                                await CollectWorkerCompletionAsync(
                                    active);

                            await active.DisposeAsync();

                            active = null;

                            SetActiveGeneration(
                                null);
                        }
                    }
                    else
                    {
                        await WaitForWakeOrDelayAsync(
                            GetRecoveryDelay(
                                consecutiveFailures),
                            cancellationToken);
                    }

                    continue;
                }

                if (reality.TargetAvailability ==
                    TargetEndpointAvailability.Absent)
                {
                    pendingWorkerFailure =
                        null;

                    consecutiveFailures =
                        0;

                    SetLastError(
                        null);

                    if (active is not null)
                    {
                        _lifecycleLog.Write(
                            "WORKER_STOP",
                            $"generation={active.Generation}; reason=endpoint_absent");

                        await StopGenerationAsync(
                            active,
                            cancellationToken);

                        active = null;

                        SetActiveGeneration(
                            null);
                    }

                    PublishState(
                        SupervisorState.WaitingForEndpoint);

                    await WaitForWakeOrDelayAsync(
                        TopologySafetyProbeInterval,
                        cancellationToken);

                    continue;
                }

                bool topologyBlocked =
                    reality.TargetAvailability ==
                        TargetEndpointAvailability.Ambiguous ||
                    !reality.DefaultRenderSafe;

                if (topologyBlocked)
                {
                    pendingWorkerFailure =
                        null;

                    consecutiveFailures =
                        0;

                    SetLastError(
                        reality.BlockingReason ??
                        "Audio endpoint topology is not eligible for routing.");

                    if (active is not null)
                    {
                        _lifecycleLog.Write(
                            "WORKER_STOP",
                            $"generation={active.Generation}; reason=topology_blocked");

                        await StopGenerationAsync(
                            active,
                            cancellationToken);

                        active = null;

                        SetActiveGeneration(
                            null);
                    }

                    PublishState(
                        SupervisorState.TopologyBlocked);

                    await WaitForWakeOrDelayAsync(
                        TopologySafetyProbeInterval,
                        cancellationToken);

                    continue;
                }

                if (pendingWorkerFailure is not null)
                {
                    consecutiveFailures++;

                    SetLastError(
                        pendingWorkerFailure);

                    pendingWorkerFailure =
                        null;

                    PublishState(
                        SupervisorState.RecoveringFault);

                    await WaitForWakeOrDelayAsync(
                        GetRecoveryDelay(
                            consecutiveFailures),
                        cancellationToken);

                    continue;
                }

                bool generationIsStale =
                    active is not null &&
                    (active.PowerRevision !=
                        power.Revision ||
                     active.RestartRevision !=
                        desired.RestartRevision ||
                     active.Configuration !=
                        desired.Configuration);

                if (generationIsStale)
                {
                    string replacementReason =
                        active!.PowerRevision !=
                            power.Revision
                            ? "power_resume"
                            : active.Configuration !=
                                desired.Configuration
                                ? "configuration"
                                : "manual_restart";

                    _lifecycleLog.Write(
                        "WORKER_REPLACE",
                        $"generation={active.Generation}; reason={replacementReason}");

                    PublishState(
                        SupervisorState.Restarting);

                    await StopGenerationAsync(
                        active,
                        cancellationToken);

                    active = null;

                    SetActiveGeneration(
                        null);
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
                                power.Revision,
                                cancellationToken);

                        consecutiveFailures =
                            0;

                        SetActiveGeneration(
                            active.Generation);

                        SetLastError(
                            null);

                        _lifecycleLog.Write(
                            "WORKER_RUNNING",
                            $"generation={active.Generation}; " +
                            $"mode={active.Configuration.Mode}; " +
                            $"powerRevision={active.PowerRevision}");

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
                        consecutiveFailures++;

                        SetLastError(
                            ex.Message);

                        PublishState(
                            SupervisorState.RecoveringFault);

                        await WaitForWakeOrDelayAsync(
                            GetRecoveryDelay(
                                consecutiveFailures),
                            cancellationToken);

                        continue;
                    }
                }

                bool completed =
                    await WaitForWorkerOrWakeAsync(
                        active,
                        cancellationToken);

                if (!completed)
                {
                    continue;
                }

                pendingWorkerFailure =
                    await CollectWorkerCompletionAsync(
                        active);

                await active.DisposeAsync();

                active = null;

                SetActiveGeneration(
                    null);
            }
        }
        finally
        {
            if (active is not null)
            {
                try
                {
                    _lifecycleLog.Write(
                        "WORKER_STOP",
                        $"generation={active.Generation}; reason=app_exit");

                    await StopGenerationAsync(
                        active,
                        CancellationToken.None);
                }
                catch
                {
                }

                SetActiveGeneration(
                    null);
            }

            if (observer is not null)
            {
                observer.TopologyChanged -=
                    OnEndpointTopologyChanged;

                observer.Dispose();
            }

            probe?.Dispose();
        }
    }

    private void OnEndpointTopologyChanged(
        object? sender,
        EventArgs e)
    {
        // NAudio is configured to deliver this callback on the Windows
        // Core Audio worker thread. Never enumerate endpoints or run policy
        // here; only wake the supervisor.
        Wake();
    }

    private async Task<WorkerGeneration>
        StartGenerationAsync(
            ConfigurationSnapshot desired,
            long powerRevision,
            CancellationToken cancellationToken)
    {
        long generation =
            Interlocked.Increment(
                ref _nextBackendGeneration);

        string pipeName =
            $"LEAudioRelay.Backend." +
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
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            handshakeCts.CancelAfter(
                WorkerHandshakeTimeout);

            await pipe.WaitForConnectionAsync(
                handshakeCts.Token);

            var reader =
                new StreamReader(
                    pipe);

            var writer =
                new StreamWriter(
                    pipe)
                {
                    AutoFlush = true
                };

            WorkerEnvelope hello =
                await ReadMessageWithTimeoutAsync(
                    reader,
                    WorkerHandshakeTimeout,
                    cancellationToken);

            if (hello.Type !=
                    WorkerProtocol.Hello ||
                hello.Generation !=
                    generation)
            {
                throw new InvalidDataException(
                    "Backend worker returned an invalid HELLO handshake.");
            }

            await WaitForRunningAsync(
                reader,
                generation,
                cancellationToken);

            Task<string?> completion =
                MonitorWorkerAsync(
                    reader,
                    process,
                    generation,
                    cancellationToken);

            return new WorkerGeneration(
                generation,
                desired.RestartRevision,
                powerRevision,
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
                TryKill(
                    process);

                process.Dispose();
            }

            pipe.Dispose();

            throw;
        }
    }

    private static async Task WaitForRunningAsync(
        StreamReader reader,
        long generation,
        CancellationToken cancellationToken)
    {
        DateTime deadline =
            DateTime.UtcNow +
            WorkerStartupTimeout;

        while (true)
        {
            TimeSpan remaining =
                deadline -
                DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Backend generation {generation} did not reach RUNNING.");
            }

            WorkerEnvelope message =
                await ReadMessageWithTimeoutAsync(
                    reader,
                    remaining,
                    cancellationToken);

            if (message.Generation !=
                generation)
            {
                continue;
            }

            if (message.Type ==
                WorkerProtocol.Running)
            {
                return;
            }

            if (message.Type ==
                WorkerProtocol.Faulted)
            {
                throw new InvalidOperationException(
                    message.Detail ??
                    $"Backend generation {generation} faulted during startup.");
            }

            if (message.Type ==
                WorkerProtocol.Stopped)
            {
                throw new InvalidOperationException(
                    $"Backend generation {generation} stopped during startup.");
            }
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
            info.FileName =
                processPath;

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
            info.FileName =
                processPath;
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

        return Process.Start(
                   info)
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

            if (message.Generation !=
                generation)
            {
                continue;
            }

            if (message.Type ==
                WorkerProtocol.Faulted)
            {
                return
                    message.Detail ??
                    $"Backend generation {generation} reported a fault.";
            }

            if (message.Type ==
                WorkerProtocol.Stopped)
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
        bool shutdownCommandDelivered =
            false;

        try
        {
            Task shutdownWrite =
                generation.Writer.WriteLineAsync(
                    WorkerProtocol.Serialize(
                        new WorkerEnvelope(
                            WorkerProtocol.Shutdown,
                            generation.Generation)));

            await shutdownWrite.WaitAsync(
                WorkerShutdownCommandTimeout,
                cancellationToken);

            shutdownCommandDelivered =
                true;
        }
        catch
        {
            // Shutdown is a courtesy request. A broken worker/control pipe
            // must never be allowed to hold the supervisor loop indefinitely.
        }

        if (shutdownCommandDelivered)
        {
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
        }
        else
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

    private async Task<string>
        CollectWorkerCompletionAsync(
            WorkerGeneration generation)
    {
        string? failure =
            await ReadCompletionFailureAsync(
                generation.Completion);

        string result =
            failure ??
            $"Backend generation {generation.Generation} stopped unexpectedly.";

        _lifecycleLog.Write(
            "WORKER_EXIT",
            $"generation={generation.Generation}; reason={result}");

        return result;
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

    private async Task<bool> WaitForWorkerOrWakeAsync(
        WorkerGeneration generation,
        CancellationToken cancellationToken)
    {
        using var waitCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        Task wakeTask =
            _wakeSignal.WaitAsync(
                waitCts.Token);

        Task completed =
            await Task.WhenAny(
                generation.Completion,
                wakeTask);

        waitCts.Cancel();

        await IgnoreCancellationAsync(
            wakeTask);

        return ReferenceEquals(
            completed,
            generation.Completion);
    }

    private async Task<bool>
        WaitForWorkerWakeOrDelayAsync(
            WorkerGeneration generation,
            TimeSpan delay,
            CancellationToken cancellationToken)
    {
        using var waitCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        Task wakeTask =
            _wakeSignal.WaitAsync(
                waitCts.Token);

        Task delayTask =
            Task.Delay(
                delay,
                waitCts.Token);

        Task completed =
            await Task.WhenAny(
                generation.Completion,
                wakeTask,
                delayTask);

        waitCts.Cancel();

        await IgnoreCancellationAsync(
            wakeTask);

        await IgnoreCancellationAsync(
            delayTask);

        return ReferenceEquals(
            completed,
            generation.Completion);
    }

    private async Task WaitForWakeOrDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        using var waitCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        Task wakeTask =
            _wakeSignal.WaitAsync(
                waitCts.Token);

        Task delayTask =
            Task.Delay(
                delay,
                waitCts.Token);

        await Task.WhenAny(
            wakeTask,
            delayTask);

        waitCts.Cancel();

        await IgnoreCancellationAsync(
            wakeTask);

        await IgnoreCancellationAsync(
            delayTask);
    }

    private static async Task IgnoreCancellationAsync(
        Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static TimeSpan GetRecoveryDelay(
        int consecutiveFailures)
    {
        int index =
            Math.Clamp(
                consecutiveFailures - 1,
                0,
                RecoveryBackoff.Length - 1);

        return RecoveryBackoff[index];
    }

    private PowerSnapshot SnapshotPower()
    {
        lock (_gate)
        {
            return _powerSnapshot;
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

    private void Wake()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Coalesce topology/configuration wake-ups.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task WaitForWakeAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _wakeSignal.WaitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void LogPowerTransition(
        PowerSnapshot current,
        ref PowerSnapshot? previous)
    {
        if (previous is not
            PowerSnapshot old)
        {
            previous =
                current;

            return;
        }

        if (current.SuspendCount !=
            old.SuspendCount)
        {
            _lifecycleLog.Write(
                "POWER_SUSPEND",
                $"suspendCount={current.SuspendCount}; " +
                $"revision={current.Revision}; " +
                $"reconciledWhileSuspended={current.IsSuspended}");
        }

        if (current.Revision !=
            old.Revision)
        {
            _lifecycleLog.Write(
                "POWER_RESUME",
                $"suspendCount={current.SuspendCount}; " +
                $"revision={current.Revision}");
        }

        previous =
            current;
    }

    private void LogEndpointTransition(
        AudioEndpointSnapshot reality,
        ref string? previousKey)
    {
        string key =
            $"{reality.TargetAvailability}|" +
            $"{reality.TargetDeviceId}|" +
            $"{reality.DefaultRenderSafe}|" +
            $"{reality.DefaultRenderName}|" +
            $"{reality.BlockingReason}";

        if (string.Equals(
                previousKey,
                key,
                StringComparison.Ordinal))
        {
            return;
        }

        previousKey =
            key;

        string eventName =
            reality.TargetAvailability switch
            {
                TargetEndpointAvailability.Absent =>
                    "ENDPOINT_ABSENT",

                TargetEndpointAvailability.Ambiguous =>
                    "ENDPOINT_AMBIGUOUS",

                _ when !reality.DefaultRenderSafe =>
                    "TOPOLOGY_BLOCKED",

                _ =>
                    "ENDPOINT_AVAILABLE"
            };

        _lifecycleLog.Write(
            eventName,
            $"target={reality.TargetFriendlyName ?? "<none>"}; " +
            $"default={reality.DefaultRenderName ?? "<none>"}; " +
            $"safeDefault={reality.DefaultRenderSafe}");
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
            _lastError =
                error;
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
                _state !=
                state;

            _state =
                state;
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

    private static void TryKill(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);

                process.WaitForExit(
                    1000);
            }
        }
        catch
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
            long powerRevision,
            RouteGenerationConfiguration configuration,
            Process process,
            NamedPipeServerStream pipe,
            StreamReader reader,
            StreamWriter writer,
            Task<string?> completion)
        {
            Generation =
                generation;

            RestartRevision =
                restartRevision;

            PowerRevision =
                powerRevision;

            Configuration =
                configuration;

            Process =
                process;

            Pipe =
                pipe;

            Reader =
                reader;

            Writer =
                writer;

            Completion =
                completion;
        }

        public long Generation
        {
            get;
        }

        public long RestartRevision
        {
            get;
        }

        public long PowerRevision
        {
            get;
        }

        public RouteGenerationConfiguration Configuration
        {
            get;
        }

        public Process Process
        {
            get;
        }

        public NamedPipeServerStream Pipe
        {
            get;
        }

        public StreamReader Reader
        {
            get;
        }

        public StreamWriter Writer
        {
            get;
        }

        public Task<string?> Completion
        {
            get;
        }

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
