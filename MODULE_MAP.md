# LE Audio Router — Module Map

Status: **Round4 active**

This module map follows ownership rather than implementation technology.

| Module | Responsibility |
|---|---|
| `Host/` | Select process role and own only process-entry concerns. |
| `Shell/` | Own Windows tray lifetime and user interaction. |
| `Settings/` | Own the small frontend-neutral Router configuration and immutable generation snapshots. |
| `Supervision/` | Own automatic backend lifecycle policy, generation replacement, worker-process monitoring, and the local typed worker protocol. |
| `Routing/` | Own one worker-local audio route generation: endpoint validation, Process Loopback source, persistent Buds render, and ordered route shutdown. |
| `Timing/` | Own the current minimal SPSC PCM boundary and startup cushion / real-zero keepalive behavior. Clock synchronization is intentionally not implemented yet. |
| `Telemetry/` | Own cheap worker-local counters with separate audible/silent drop semantics. |
| `Cli/` | Thin command-line adapter. It must not own router policy or audio behavior. |
| `Legacy/V0.1/` | Frozen historical implementation and VF-KB evidence. Never a production dependency. |

## Product-level lifecycle rule

```text
application alive
    => routing intent
    => supervisor maintains one healthy route generation

application exit
    => stop generation
    => stop supervisor
    => exit process
```

There is no independent Enabled state and no configurable reconnect policy.

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
LEAudioRouter.csproj
    includes Program.cs
    includes Host/**
    includes Shell/**
    includes Settings/**
    includes Supervision/**
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
| `Lifecycle/` | Windows power and endpoint observations. Observers report facts; they do not own recovery policy. |
| `Timing/` control layer | Drift observation, active recentering, and later clock synchronization on top of the existing PCM boundary. |
| `Diagnostics/` | Explicit experiments such as latency and future drift probes. |

## Dependency direction

```text
Shell ───────► Settings
  │
  └──────────► Supervision

Cli ─────────► future shell control boundary

Supervision ─► worker process protocol
Supervision ─► future Lifecycle observations

worker Host ─► Routing
Routing ─────► Timing
Routing ─────► Telemetry
Timing ──────► Telemetry
```

The Shell must never directly own WASAPI clients, ring pointers, capture callbacks, or route-local recovery internals.
