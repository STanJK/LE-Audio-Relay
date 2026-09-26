# ADR 0004 — Temporary positive-drift guard for daily-use validation

**Status:** Accepted as provisional  
**Date:** 2026-09-27

## Context

The current Process Loopback producer and Buds render consumer do not share an explicitly synchronized application-level clock.

Long runs can therefore accumulate positive ring drift until the SPSC ring reaches capacity. The final design may use drift estimation, recentering, sample slip with smoothing, PLL-style control, or ASRC.

That full Timing work is intentionally deferred, but the daily-use validation baseline needs to avoid a permanently saturated ring.

## Decision

Add a deliberately narrow `ProvisionalPositiveDriftGuard`.

It corrects only positive drift.

### Preferred correction: silence suppression

When Process Loopback marks incoming packets Silent and the ring is above the normal target cushion, excess silent frames are not inserted into the ring.

The render path still produces real zero PCM, so this is the preferred correction path.

### Continuous-audio correction

If the ring reaches the high-water band during uninterrupted audio, discard one complete stereo frame every four render callbacks until fill returns below the low-water band.

Both channels are always slipped together.

### Emergency correction

If fill reaches the emergency band close to capacity, discard enough queued frames to return to the high-water band.

This is a last-resort guardrail, not normal control behavior.

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
- guarantee artifact-free emergency trim.

It exists only to make the current branch comfortable enough for sustained daily validation until the proper Timing controller is designed.
