# LE Audio Relay — Module Map

Status: **Round4 active**

This module map follows ownership rather than implementation technology.

| Module | Responsibility |
|---|---|
| `Host/` | Select process role and own only process-entry concerns. |
| `Shell/` | Own Windows tray lifetime and user interaction. |
| `Settings/` | Own the small frontend-neutral Router configuration and immutable generation snapshots. |
| `Supervision/` | Own route-generation policy: reconcile configuration + observed Windows reality into whether a worker should exist. |
| `Lifecycle/` | Observe Windows endpoint topology and Windows suspend/resume. It reports facts and wake-ups; it does not start workers or own route teardown. |
| `Routing/` | Own one worker-local audio route generation: endpoint validation, Process Loopback source, persistent Buds render, and ordered route shutdown. |
| `Timing/` | Own the SPSC PCM boundary, startup cushion / real-zero keepalive, and the temporary positive-drift guard. Full clock synchronization is intentionally not implemented yet. |
| `Telemetry/` | Own worker-local counters plus the low-volume persistent lifecycle journal. Lifecycle logs explicitly exclude heartbeat/ring-warning spam. |
| `Cli/` | Thin command-line adapter. It must not own router policy or audio behavior. |
| `Legacy/V0.1/` | Frozen historical implementation and VF-KB evidence. Never a production dependency. |

## Product-level lifecycle rule

```text
application alive
    => routing intent

target endpoint absent
    => zero workers
    => wait for topology change

target endpoint active + default route safe
    => one healthy worker should exist

route failure while topology remains eligible
    => bounded recovery retry
```

There is no independent Enabled state and no configurable reconnect policy.

## Event-driven endpoint reconciliation

`AudioEndpointObserver` subscribes to NAudio/Core Audio endpoint notifications using `useSynchronizationContext: false`.

Its callback contract is intentionally tiny:

```text
Core Audio notification thread
    => coalesced Wake()
    => return immediately
```

The callback never enumerates endpoints, never disposes audio objects, and never starts/stops a worker.

After wake-up, `AudioEndpointProbe` re-enumerates active render endpoints and the default multimedia render endpoint. That re-enumeration is the source of truth.

A 30-second low-frequency safety probe exists only to recover from a missed notification. It does not create a worker while the target remains absent.

See `docs/decisions/0002-event-driven-endpoint-reconciliation.md`.

## Process boundary

Exactly two runtime process roles are permitted:

```text
Tray / Supervisor process
Audio Route Worker process
```

The worker is one disposable route-generation fault/diagnostic boundary.

Inside the worker, `Routing/`, `Timing/`, and `Telemetry/` are ordinary in-process modules. They are not separate process roles.

See `docs/decisions/0001-out-of-process-route-generation.md`.

## Current audio path

```text
Windows applications
    ↓
sacrificial physical default render sink
    ↓
Process Loopback source
    ↓
Timing/PcmRelayBoundary
    ↓
persistent Buds render sink
```

The current Timing boundary preserves the validated V0.1 startup semantics:

- one SPSC ring;
- bounded startup accumulation;
- request + target-cushion activation;
- real-zero keepalive before relay activation;
- real-zero fill when runtime data is missing.

It does **not** yet perform drift estimation, recentering, sample slip, PLL, or ASRC.

## Build boundary

The root project disables the SDK's recursive default C# item discovery and explicitly compiles only Round4 production modules.

```text
LEAudioRelay.csproj
    includes Program.cs
    includes Host/**
    includes Shell/**
    includes Settings/**
    includes Supervision/**
    includes Lifecycle/**
    includes Routing/**
    includes Timing/**
    includes Telemetry/**
    includes Cli/**

    DOES NOT include Legacy/**
```

Archived implementations and generated `bin/` / `obj/` trees must never become accidental dependencies of the canonical application.

## Reserved next modules

| Module | Future responsibility |
|---|---|
| `Timing/` control layer | Drift observation, active recentering, and later clock synchronization on top of the existing PCM boundary. |
| `Diagnostics/` | Explicit experiments such as latency and future drift probes. |

## Dependency direction

```text
Shell ───────► Settings
  │
  └──────────► Supervision

Cli ─────────► future shell control boundary

Supervision ─► Lifecycle
Supervision ─► worker process protocol

worker Host ─► Routing
Routing ─────► Timing
Routing ─────► Telemetry
Timing ──────► Telemetry
```

The Shell must never directly own WASAPI clients, ring pointers, capture callbacks, or route-local recovery internals.


## Power lifecycle invariant

```text
PBT_APMSUSPEND
    => record Suspended fact
    => wake Supervisor
    => do NOT touch WASAPI in the callback

PBT_APMRESUME*
    => Awake
    => PowerRevision += 1
    => wake Supervisor

any worker whose PowerRevision != current PowerRevision
    => stale generation
    => replace before trusting audio again
```

A route generation is never trusted across a suspend/resume cycle.

## Temporary positive-drift guard

The current Timing layer contains `ProvisionalPositiveDriftGuard`.

It is explicitly not a clock synchronizer. Its daily-use purpose is only to prevent slow positive producer/consumer drift from leaving the ring permanently full.

It controls the post-render residual queue around the 10 ms target cushion:

```text
target residual       10 ms
gradual trim stops    11 ms
gradual trim starts   12 ms
hard recenter         20 ms
physical capacity     80 ms
```

It corrects in this order:

1. suppress already-accumulated excess during source-declared silence;
2. during uninterrupted audio, discard one complete stereo frame every eight render callbacks only while post-render residual fill remains above the 11–12 ms hysteresis band;
3. if residual fill reaches 20 ms, hard-recenter directly to the 10 ms target.

The 80 ms ring size is physical safety headroom, not a normal operating latency target.

Intentional correction frames have separate telemetry counters and are not counted as runtime drop/overflow.
