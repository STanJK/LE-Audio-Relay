namespace LEAudioRouter.Timing;

/// <summary>
/// Temporary one-sided guard for positive producer/consumer clock drift.
///
/// This is NOT clock synchronization. It only prevents slow positive drift from
/// pinning the ring at capacity until a later proper Timing controller replaces
/// it.
/// </summary>
internal sealed class ProvisionalPositiveDriftGuard
{
    private const int GradualTrimEveryRenderCallbacks = 4;

    private readonly int _targetFrames;
    private readonly int _lowWaterFrames;
    private readonly int _highWaterFrames;
    private readonly int _emergencyWaterFrames;

    private bool _gradualTrimActive;
    private int _renderCallbackCounter;

    public ProvisionalPositiveDriftGuard(
        int sampleRate,
        int capacityFrames,
        int targetFrames)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate));
        }

        if (capacityFrames <= 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityFrames));
        }

        _targetFrames =
            Math.Clamp(
                targetFrames,
                1,
                capacityFrames - 2);

        int lowCandidate =
            sampleRate *
            15 /
            1000;

        int highCandidate =
            sampleRate *
            30 /
            1000;

        int emergencyCandidate =
            capacityFrames -
            sampleRate *
            5 /
            1000;

        _lowWaterFrames =
            Math.Clamp(
                lowCandidate,
                _targetFrames + 1,
                capacityFrames - 2);

        _highWaterFrames =
            Math.Clamp(
                highCandidate,
                _lowWaterFrames + 1,
                capacityFrames - 1);

        _emergencyWaterFrames =
            Math.Clamp(
                emergencyCandidate,
                _highWaterFrames + 1,
                capacityFrames);
    }

    /// <summary>
    /// During source-declared silence, suppress buffered silence above the
    /// normal target. The renderer will still output real zeros, so this is the
    /// preferred inaudible way to remove accumulated positive drift.
    /// </summary>
    public int GetSilentFramesToSuppress(
        int incomingFrames,
        int currentFillFrames)
    {
        if (incomingFrames <= 0 ||
            currentFillFrames <=
                _targetFrames)
        {
            return 0;
        }

        int excess =
            currentFillFrames -
            _targetFrames;

        return Math.Min(
            incomingFrames,
            excess);
    }

    /// <summary>
    /// For uninterrupted non-silent audio, apply a very slow stereo-frame slip
    /// only after the ring crosses a high-water mark. Emergency trimming exists
    /// only to guarantee the ring cannot remain pinned near capacity.
    /// </summary>
    public DriftTrimResult ApplyBeforeRender(
        SpscPcmRing ring)
    {
        int fill =
            ring.FillFrames;

        if (fill >=
            _emergencyWaterFrames)
        {
            int requested =
                fill -
                _highWaterFrames;

            int trimmed =
                ring.Discard(
                    requested);

            _gradualTrimActive =
                trimmed == 0;

            _renderCallbackCounter =
                0;

            return new DriftTrimResult(
                GradualFrames: 0,
                EmergencyFrames: trimmed);
        }

        if (!_gradualTrimActive &&
            fill >=
                _highWaterFrames)
        {
            _gradualTrimActive =
                true;

            _renderCallbackCounter =
                0;
        }

        if (!_gradualTrimActive)
        {
            return default;
        }

        if (fill <=
            _lowWaterFrames)
        {
            _gradualTrimActive =
                false;

            _renderCallbackCounter =
                0;

            return default;
        }

        _renderCallbackCounter++;

        if (_renderCallbackCounter <
            GradualTrimEveryRenderCallbacks)
        {
            return default;
        }

        _renderCallbackCounter =
            0;

        int gradual =
            ring.Discard(
                1);

        return new DriftTrimResult(
            GradualFrames: gradual,
            EmergencyFrames: 0);
    }
}

internal readonly record struct DriftTrimResult(
    int GradualFrames,
    int EmergencyFrames);
