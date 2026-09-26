using LEAudioRouter.Telemetry;

namespace LEAudioRouter.Timing;

internal sealed class PcmRelayBoundary
{
    private const int ModeKeepAlive = 0;
    private const int ModeArmed = 1;
    private const int ModeRelay = 2;

    private readonly SpscPcmRing _ring;
    private readonly RouteTelemetry _telemetry;
    private readonly int _blockAlign;
    private readonly int _targetCushionFrames;
    private readonly int _startupHoldFrames;

    private int _mode =
        ModeKeepAlive;

    public PcmRelayBoundary(
        int capacityFrames,
        int blockAlign,
        int targetCushionFrames,
        int startupHoldFrames,
        RouteTelemetry telemetry)
    {
        _ring =
            new SpscPcmRing(
                capacityFrames,
                blockAlign);

        _telemetry = telemetry;
        _blockAlign = blockAlign;
        _targetCushionFrames =
            targetCushionFrames;
        _startupHoldFrames =
            startupHoldFrames;
    }

    public int FillFrames =>
        _ring.FillFrames;

    public bool RelayActive =>
        Volatile.Read(
            ref _mode) ==
        ModeRelay;

    public void Arm() =>
        Interlocked.Exchange(
            ref _mode,
            ModeArmed);

    public void Disable() =>
        Interlocked.Exchange(
            ref _mode,
            ModeKeepAlive);

    public void Push(
        ReadOnlySpan<byte> buffer,
        bool silent)
    {
        int frames =
            buffer.Length /
            _blockAlign;

        if (frames <= 0)
        {
            return;
        }

        bool runtime =
            RelayActive;

        int maxFillFrames =
            runtime
                ? _ring.CapacityFrames
                : _startupHoldFrames;

        int written =
            silent
                ? _ring.WriteSilence(
                    frames,
                    maxFillFrames)
                : _ring.Write(
                    buffer,
                    maxFillFrames);

        _telemetry.RecordCapture(
            frames,
            silent,
            written,
            runtime);
    }

    public int FillRenderBuffer(
        Span<byte> buffer)
    {
        buffer.Clear();

        int requestedFrames =
            buffer.Length /
            _blockAlign;

        if (requestedFrames <= 0)
        {
            return buffer.Length;
        }

        int mode =
            Volatile.Read(
                ref _mode);

        if (mode == ModeArmed)
        {
            int requiredFill =
                requestedFrames +
                _targetCushionFrames;

            int observedFill =
                _ring.FillFrames;

            if (observedFill >= requiredFill)
            {
                if (Interlocked.CompareExchange(
                        ref _mode,
                        ModeRelay,
                        ModeArmed) ==
                    ModeArmed)
                {
                    int excess =
                        observedFill -
                        requiredFill;

                    int trimmed =
                        _ring.Discard(
                            excess);

                    _telemetry.RecordStartupTrim(
                        trimmed);
                }

                mode =
                    Volatile.Read(
                        ref _mode);
            }
        }

        if (mode != ModeRelay)
        {
            _telemetry.RecordRender(
                requestedFrames,
                readFrames: 0,
                keepAlive: true);

            return buffer.Length;
        }

        int readFrames =
            _ring.Read(
                buffer,
                requestedFrames);

        _telemetry.RecordRender(
            requestedFrames,
            readFrames,
            keepAlive: false);

        return buffer.Length;
    }
}
