using LEKeepAliveRelay.Audio;
using LEKeepAliveRelay.Config;

namespace LEKeepAliveRelay.Runtime;

internal enum HealthSeverity
{
    Warning,
    Error
}

internal readonly record struct HealthEvent(
    HealthSeverity Severity,
    string Message);

internal sealed class RuntimeHealthFilter
{
    private readonly int _normalFillUpperFrames;
    private readonly int _sustainedFillAlertSeconds;
    private readonly int _majorGlitchFrames;

    private long _previousCapturePackets;
    private long _previousCaptureFrames;
    private long _previousRenderFrames;
    private long _previousUnderrun;
    private long _previousOverflow;
    private long _previousCallbackErrors;

    private long _pendingUnderrun;
    private long _pendingOverflow;

    private int _highFillStreak;
    private int _emptyWhileActiveStreak;
    private int _slowRenderStreak;

    private bool _highFillAlerted;
    private bool _emptyAlerted;
    private bool _slowRenderAlerted;

    public RuntimeHealthFilter(RelayOptions options)
    {
        _normalFillUpperFrames =
            options.SampleRate * options.NormalFillUpperMs / 1000;

        _sustainedFillAlertSeconds =
            options.SustainedFillAlertSeconds;

        _majorGlitchFrames =
            options.MajorGlitchFrames;
    }


    public void Prime(
        SpscPcmRing ring,
        RingRenderProvider provider,
        RelayRuntimeCounters counters)
    {
        _previousCapturePackets = Interlocked.Read(ref counters.CapturePackets);
        _previousCaptureFrames = Interlocked.Read(ref counters.CaptureFrames);
        _previousRenderFrames = provider.FramesReadFromRing;
        _previousUnderrun = ring.UnderrunFrames;
        _previousOverflow = ring.OverflowFrames;
        _previousCallbackErrors = Interlocked.Read(ref counters.CallbackErrors);

        _pendingUnderrun = 0;
        _pendingOverflow = 0;
        _highFillStreak = 0;
        _emptyWhileActiveStreak = 0;
        _slowRenderStreak = 0;
        _highFillAlerted = false;
        _emptyAlerted = false;
        _slowRenderAlerted = false;
    }

    public IReadOnlyList<HealthEvent> Observe(
        SpscPcmRing ring,
        RingRenderProvider provider,
        RelayRuntimeCounters counters)
    {
        var events = new List<HealthEvent>(4);

        long packets = Interlocked.Read(ref counters.CapturePackets);
        long captured = Interlocked.Read(ref counters.CaptureFrames);
        long rendered = provider.FramesReadFromRing;
        long underrun = ring.UnderrunFrames;
        long overflow = ring.OverflowFrames;
        long callbackErrors = Interlocked.Read(ref counters.CallbackErrors);

        long packetRate = packets - _previousCapturePackets;
        long captureRate = captured - _previousCaptureFrames;
        long renderRate = rendered - _previousRenderFrames;
        long underDelta = underrun - _previousUnderrun;
        long overDelta = overflow - _previousOverflow;
        long callbackDelta = callbackErrors - _previousCallbackErrors;

        _previousCapturePackets = packets;
        _previousCaptureFrames = captured;
        _previousRenderFrames = rendered;
        _previousUnderrun = underrun;
        _previousOverflow = overflow;
        _previousCallbackErrors = callbackErrors;

        if (callbackDelta > 0)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Error,
                $"Process-loopback callback errors +{callbackDelta} (total {callbackErrors})."));
        }

        // Ignore tiny one-off glitches. Report only once they accumulate to >= 10 ms.
        if (underDelta > 0)
        {
            _pendingUnderrun += underDelta;
        }

        if (_pendingUnderrun >= _majorGlitchFrames)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Warning,
                $"Audio underrun accumulated {_pendingUnderrun} frames " +
                $"({_pendingUnderrun * 1000.0 / 48_000:F1} ms)."));

            _pendingUnderrun = 0;
        }

        if (overDelta > 0)
        {
            _pendingOverflow += overDelta;
        }

        if (_pendingOverflow >= _majorGlitchFrames)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Warning,
                $"Audio overflow accumulated {_pendingOverflow} frames " +
                $"({_pendingOverflow * 1000.0 / 48_000:F1} ms)."));

            _pendingOverflow = 0;
        }

        int fill = ring.FillFrames;

        // 10 <-> 20 ms phase movement is normal. Even 30 ms is tolerated silently.
        if (fill > _normalFillUpperFrames)
        {
            _highFillStreak++;
        }
        else
        {
            _highFillStreak = 0;
            _highFillAlerted = false;
        }

        if (_highFillStreak >= _sustainedFillAlertSeconds && !_highFillAlerted)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Warning,
                $"Ring latency stayed high for {_highFillStreak}s: " +
                $"{fill} frames ({fill * 1000.0 / 48_000:F1} ms)."));

            _highFillAlerted = true;
        }

        bool activelyFlowing = packetRate >= 50 && captureRate >= 24_000;

        if (provider.RelayEnabled && activelyFlowing && fill == 0)
        {
            _emptyWhileActiveStreak++;
        }
        else
        {
            _emptyWhileActiveStreak = 0;
            _emptyAlerted = false;
        }

        if (_emptyWhileActiveStreak >= 2 && !_emptyAlerted)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Warning,
                "Ring remained empty while Process Loopback was actively producing audio."));

            _emptyAlerted = true;
        }

        if (provider.RelayEnabled && activelyFlowing && renderRate < 24_000)
        {
            _slowRenderStreak++;
        }
        else
        {
            _slowRenderStreak = 0;
            _slowRenderAlerted = false;
        }

        if (_slowRenderStreak >= 3 && !_slowRenderAlerted)
        {
            events.Add(new HealthEvent(
                HealthSeverity.Error,
                $"Destination render appears stalled/slow: {renderRate} frames/s."));

            _slowRenderAlerted = true;
        }

        return events;
    }
}
