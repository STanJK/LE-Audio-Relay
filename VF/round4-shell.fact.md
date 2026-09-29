# LE Audio Relay Current VF-KB — Cross-layer Rationale

Canonical implementation snapshot for this page: `da3217f76a0632bdaf6fea25e9103dbef0298137`.

The individual `.vf.md` nodes are the canonical machine-readable semantics. This page intentionally contains only cross-layer reasoning that would otherwise be repeated across several nodes.

## Two lifetimes, not one

V0.1 made product lifetime and route lifetime effectively the same thing. Round4 separates them: the tray/supervisor is durable, while one worker PID represents one disposable route generation. That lets endpoint loss, resume, manual restart, or route failure replace process-local audio state without restarting the product.

The worker boundary is diagnostic and user-mode fault containment only. It cannot isolate kernel crashes or state that lives below the process boundary.

## Observers report facts; supervision owns policy

Core Audio and WM_POWERBROADCAST callbacks are deliberately tiny. They wake supervision or update a fact snapshot and return. They do not enumerate topology, tear down WASAPI objects, or spawn workers.

This keeps event-thread behavior deterministic and gives the supervisor one place to reconcile endpoint reality, power epoch, configuration, and generation health.

## Worker liveness is an architectural invariant

A disposable worker is only useful if the supervisor can abandon it without cooperation. Shutdown IPC is therefore bounded, graceful completion is bounded, and forced process-tree termination is authoritative. The control pipe is broken before wrapper cleanup so dead worker I/O cannot pin reconciliation.

This rule was made explicit after daily testing exposed a failure mode where abrupt earbud removal could leave the UI alive while the supervisor was blocked trying to stop the old worker.

## Process Loopback and the sacrificial/default endpoint are separate concepts

Ordinary applications still need a Windows default render destination that is not the LE Audio target. Process Loopback, however, is process-scoped and is not capturing from that endpoint. The current default-output check is a temporary safety heuristic around the Daily 2 name-based configuration, not a general physical-endpoint classifier.

## Timing is intentionally layered

`PcmRelayBoundary` owns transport invariants: zero-backed rendering, KEEPALIVE/ARMED/RELAY startup, the 10 ms retained cushion, and explicit drop semantics.

`ProvisionalPositiveDriftGuard` is a child policy that only mitigates positive fill drift. Keeping it separate prevents a temporary 11/12/20 ms heuristic from becoming confused with future PLL/ASRC or bidirectional clock synchronization.

## Historical identity

Current product/source identity is LE Audio Relay, but existing VF node IDs keep the `leaudio-router.*` prefix. Those IDs are stable historical knowledge identifiers and are not renamed merely to follow branding.

## Historical evidence stays frozen

`Legacy/V0.1/VF/` remains a sealed projection of the old implementation and old source revisions. Later knowledge may explain why V0.1 behaved as it did, but that understanding must not rewrite what the V0.1 nodes claimed about their own implementation snapshot.
