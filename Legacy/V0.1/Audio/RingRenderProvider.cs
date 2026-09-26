using NAudio.Wave;

namespace LEKeepAliveRelay.Audio;

internal sealed class RingRenderProvider :
    IWaveProvider
{
    private const int ModeKeepAlive = 0;
    private const int ModeArmed = 1;
    private const int ModeRelay = 2;

    private readonly SpscPcmRing _ring;
    private readonly int _blockAlign;
    private readonly int _targetCushionFrames;

    private int _mode;

    private long _framesReadFromRing;
    private long _underrunZeroFillFrames;
    private long _keepAliveFrames;
    private long _startupDiscardFrames;

    private int _activationObservedFill;

    public RingRenderProvider(
        WaveFormat format,
        SpscPcmRing ring,
        int targetCushionFrames)
    {
        WaveFormat =
            format;

        _ring =
            ring;

        _blockAlign =
            format.BlockAlign;

        _targetCushionFrames =
            targetCushionFrames;

        _mode =
            ModeKeepAlive;
    }

    public WaveFormat WaveFormat
    {
        get;
    }

    public bool RelayEnabled =>
        Volatile.Read(
            ref _mode) ==
        ModeRelay;

    public string ModeName =>
        Volatile.Read(
            ref _mode) switch
        {
            ModeKeepAlive => "KEEPALIVE",
            ModeArmed => "ARMED",
            ModeRelay => "RELAY",
            _ => "UNKNOWN"
        };

    public long FramesReadFromRing =>
        Interlocked.Read(
            ref _framesReadFromRing);

    public long UnderrunZeroFillFrames =>
        Interlocked.Read(
            ref _underrunZeroFillFrames);

    public long KeepAliveFrames =>
        Interlocked.Read(
            ref _keepAliveFrames);

    public long StartupDiscardFrames =>
        Interlocked.Read(
            ref _startupDiscardFrames);

    public int ActivationObservedFill =>
        Volatile.Read(
            ref _activationObservedFill);

    public void ArmRelay()
    {
        Interlocked.Exchange(
            ref _mode,
            ModeArmed);
    }

    public void DisableRelay()
    {
        Interlocked.Exchange(
            ref _mode,
            ModeKeepAlive);
    }

    public int Read(
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

        if (mode ==
            ModeArmed)
        {
            /*
             * Important startup fix:
             *
             * The first destination render request may be larger than one
             * 480-frame quantum (Buds commonly exposes a 1056-frame endpoint
             * buffer). Do not switch to RELAY until the ring contains:
             *
             *     current render request + 10 ms cushion
             *
             * Then trim any excess and consume the request in the same call.
             * This leaves exactly the target cushion and prevents the old
             * 96-frame first-read underrun.
             */
            int requiredFill =
                requestedFrames +
                _targetCushionFrames;

            int observedFill =
                _ring.FillFrames;

            if (observedFill >=
                requiredFill)
            {
                if (Interlocked.CompareExchange(
                        ref _mode,
                        ModeRelay,
                        ModeArmed) ==
                    ModeArmed)
                {
                    Volatile.Write(
                        ref _activationObservedFill,
                        observedFill);

                    int excess =
                        observedFill -
                        requiredFill;

                    if (excess > 0)
                    {
                        int discarded =
                            _ring.Discard(
                                excess);

                        Interlocked.Add(
                            ref _startupDiscardFrames,
                            discarded);
                    }
                }

                mode =
                    Volatile.Read(
                        ref _mode);
            }
        }

        if (mode !=
            ModeRelay)
        {
            Interlocked.Add(
                ref _keepAliveFrames,
                requestedFrames);

            return buffer.Length;
        }

        Span<byte> target =
            buffer.Slice(
                0,
                requestedFrames *
                _blockAlign);

        int readFrames =
            _ring.ReadRuntime(
                target,
                requestedFrames);

        if (readFrames > 0)
        {
            Interlocked.Add(
                ref _framesReadFromRing,
                readFrames);
        }

        int missing =
            requestedFrames -
            readFrames;

        if (missing > 0)
        {
            Interlocked.Add(
                ref _underrunZeroFillFrames,
                missing);
        }

        return buffer.Length;
    }
}
