using LEAudioRouter.Settings;
using LEAudioRouter.Telemetry;
using LEAudioRouter.Timing;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEAudioRouter.Routing;

internal sealed class RouteSession :
    IAsyncDisposable
{
    private const int CaptureBufferMs = 10;
    private const int RingCapacityMs = 80;
    private const int TargetCushionMs = 10;
    private const int StartupHoldMs = 40;

    private readonly AudioEndpointResolver _resolver;
    private readonly MMDevice _destination;
    private readonly PcmRelayBoundary _boundary;
    private readonly RouteTelemetry _telemetry;
    private readonly PersistentRenderSink _render;
    private readonly ProcessLoopbackSource _capture;

    private readonly TaskCompletionSource<RouteFailure>
        _failure =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

    private int _stopping;
    private int _disposed;

    private RouteSession(
        AudioEndpointResolver resolver,
        MMDevice destination,
        PcmRelayBoundary boundary,
        RouteTelemetry telemetry,
        PersistentRenderSink render,
        ProcessLoopbackSource capture)
    {
        _resolver = resolver;
        _destination = destination;
        _boundary = boundary;
        _telemetry = telemetry;
        _render = render;
        _capture = capture;

        _render.Stopped +=
            OnRenderStopped;

        _capture.Packet +=
            OnCapturePacket;

        _capture.Stopped +=
            OnCaptureStopped;
    }

    public Task<RouteFailure> Failure =>
        _failure.Task;

    public RouteTelemetrySnapshot Telemetry =>
        _telemetry.Snapshot();

    public static async Task<RouteSession> StartAsync(
        RouteGenerationConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(
                10,
                0,
                20348))
        {
            throw new PlatformNotSupportedException(
                "Process Loopback requires Windows build 20348 or newer.");
        }

        AudioEndpointResolver? resolver =
            new AudioEndpointResolver();

        MMDevice? destination = null;
        PersistentRenderSink? render = null;
        ProcessLoopbackSource? capture = null;

        try
        {
            destination =
                resolver!.FindUniqueActiveRender(
                    configuration.DestinationMatch);

            AudioFormatPolicy.ValidateDestination(
                destination);

            ValidateDefaultRouting(
                resolver!,
                configuration.DestinationMatch);

            WaveFormat format =
                AudioFormatPolicy.CreateRouteFormat();

            var telemetry =
                new RouteTelemetry();

            var boundary =
                new PcmRelayBoundary(
                    sampleRate:
                        AudioFormatPolicy.SampleRate,

                    capacityFrames:
                        AudioFormatPolicy.SampleRate *
                        RingCapacityMs /
                        1000,

                    blockAlign:
                        format.BlockAlign,

                    targetCushionFrames:
                        AudioFormatPolicy.SampleRate *
                        TargetCushionMs /
                        1000,

                    startupHoldFrames:
                        AudioFormatPolicy.SampleRate *
                        StartupHoldMs /
                        1000,

                    telemetry);

            var provider =
                new RouteRenderProvider(
                    format,
                    boundary);

            render =
                PersistentRenderSink.Create(
                    destination,
                    configuration.Mode,
                    provider);

            capture =
                await ProcessLoopbackSource
                    .CreateExcludeCurrentProcessTreeAsync(
                        format,
                        CaptureBufferMs);

            var session =
                new RouteSession(
                    resolver!,
                    destination,
                    boundary,
                    telemetry,
                    render,
                    capture);

            // Ownership transfers completely to RouteSession at this point.
            resolver = null;
            render = null;
            capture = null;
            destination = null;

            try
            {
                session._render.Start();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(250),
                    cancellationToken);

                session._boundary.Arm();

                session._capture.Start();

                return session;
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }
        catch
        {
            capture?.Dispose();
            render?.Dispose();
            destination?.Dispose();
            resolver?.Dispose();

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0)
        {
            return;
        }

        Interlocked.Exchange(
            ref _stopping,
            1);

        _boundary.Disable();

        try
        {
            _capture.Stop();
        }
        catch
        {
        }

        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(100));
        }
        catch
        {
        }

        try
        {
            _render.Stop();
        }
        catch
        {
        }

        _render.Stopped -=
            OnRenderStopped;

        _capture.Packet -=
            OnCapturePacket;

        _capture.Stopped -=
            OnCaptureStopped;

        _capture.Dispose();
        _render.Dispose();
        _destination.Dispose();
        _resolver.Dispose();
    }

    private void OnCapturePacket(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        long devicePosition,
        long qpcPosition)
    {
        try
        {
            bool silent =
                (flags &
                 AudioClientBufferFlags.Silent) !=
                0;

            _boundary.Push(
                buffer,
                silent);
        }
        catch (Exception ex)
        {
            _telemetry.RecordCallbackError();

            SignalFailure(
                "Process Loopback packet handling failed",
                ex);
        }
    }

    private void OnCaptureStopped(
        Exception? exception)
    {
        if (Volatile.Read(
                ref _stopping) != 0)
        {
            return;
        }

        SignalFailure(
            "Process Loopback capture stopped unexpectedly",
            exception);
    }

    private void OnRenderStopped(
        Exception? exception)
    {
        if (Volatile.Read(
                ref _stopping) != 0)
        {
            return;
        }

        SignalFailure(
            "Destination render stopped unexpectedly",
            exception);
    }

    private void SignalFailure(
        string reason,
        Exception? exception)
    {
        _failure.TrySetResult(
            new RouteFailure(
                reason,
                exception));
    }

    private static void ValidateDefaultRouting(
        AudioEndpointResolver resolver,
        string destinationMatch)
    {
        string defaultRender =
            resolver.GetDefaultRenderName();

        bool pointsToDestination =
            defaultRender.Contains(
                destinationMatch,
                StringComparison.OrdinalIgnoreCase) ||
            defaultRender.Contains(
                "Galaxy Buds3 Pro",
                StringComparison.OrdinalIgnoreCase);

        bool pointsToCable =
            defaultRender.Contains(
                "CABLE Input",
                StringComparison.OrdinalIgnoreCase);

        if (pointsToDestination ||
            pointsToCable)
        {
            throw new InvalidOperationException(
                "Windows default output must be a sacrificial physical sink, " +
                "not the Buds destination or CABLE Input. " +
                $"Current default: {defaultRender}");
        }
    }
}
