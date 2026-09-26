# ADR 0004 — Temporary positive-drift guard for daily-use validation

**Status:** Accepted as provisional  
**Date:** 2026-09-27

## Context

The current Process Loopback producer and Buds render consumer do not share an explicitly synchronized application-level clock.

Long runs can therefore accumulate positive ring drift until the SPSC ring reaches capacity. The physical ring currently has 80 ms of storage, but that is safety headroom only. The intended steady-state residual cushion is approximately 10 ms.

The final design may use drift estimation, recentering, sample slip with smoothing, PLL-style control, or ASRC.

That full Timing work is intentionally deferred, but the daily-use validation baseline needs to avoid both permanent ring saturation and accidental growth of normal queue latency.

## Decision

Add a deliberately narrow `ProvisionalPositiveDriftGuard`.

It corrects only positive drift and controls the **residual fill after the current render request has been read**.

For the current 48 kHz / 10 ms cushion configuration:

```text
target residual       10 ms
gradual trim stops    11 ms
gradual trim starts   12 ms
hard recenter         20 ms
physical capacity     80 ms
```

The 80 ms capacity is not part of the normal control band.

### Preferred correction: silence suppression

When Process Loopback marks incoming packets Silent and the ring already contains more than the 10 ms target cushion, the accumulated excess is suppressed from the incoming silent packet.

The render path still produces real zero PCM, so this is the preferred inaudible correction path.

### Continuous-audio correction

After each normal render read, inspect the residual queue fill.

When residual fill reaches approximately target + 2 ms, enter gradual trim mode.

While gradual trim is active and residual fill remains above approximately target + 1 ms, discard one complete stereo sample frame every eight render callbacks.

At a nominal 10 ms render cadence this provides roughly 12.5 frames/s of one-sided correction, about 260 ppm at 48 kHz.

Both channels are always slipped together.

### Hard recenter

If post-render residual fill reaches approximately target + 10 ms, discard the accumulated excess back to the 10 ms target.

For the current configuration this is a 20 ms residual threshold.

This is a last-resort daily-use guardrail, not normal control behavior.

## Why post-render residual fill

Before a render read, a healthy ring can naturally contain:

```text
10 ms target cushion
+
~10 ms current render request
=
~20 ms
```

Therefore pre-render fill is not a valid 10 ms control signal.

After the requested frames are consumed, the remaining ring fill directly represents the retained cushion and is the correct quantity to regulate around 10 ms.

## Telemetry semantics

Intentional correction has separate counters:

- ClockSilentTrimFrames;
- ClockGradualTrimFrames;
- ClockEmergencyTrimFrames.

Those frames are not counted as RuntimeDroppedAudioFrames or RuntimeDroppedSilentFrames.

## Non-goals

This ADR does not claim clock synchronization.

The provisional guard does not:

- estimate ppm drift;
- correct negative drift;
- perform crossfaded sample slips;
- implement PLL/ASRC;
- guarantee artifact-free hard recentering.

It exists only to make the current branch comfortable enough for sustained daily validation until the proper Timing controller is designed.
