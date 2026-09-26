# LE Audio Router — Module Map

Status: **Round4 bootstrap**

This module map follows ownership rather than implementation technology.

| Module | Responsibility |
|---|---|
| `Host/` | Select process mode and own only process-entry concerns. |
| `Shell/` | Own the Windows tray lifetime, user interaction, and presentation of desired/observed state. |
| `Settings/` | Own frontend-neutral desired router configuration. |
| `Supervision/` | Own backend lifecycle policy, generation replacement, worker-process monitoring, and the local typed worker protocol. |
| `Cli/` | Thin command-line adapter. It must not own router policy or audio behavior. |
| `Legacy/V0.1/` | Frozen historical implementation and VF-KB evidence. Never a production dependency. |

## Build boundary

The root project disables the SDK's recursive default C# item discovery and explicitly compiles only the Round4 production modules.

Therefore:

```text
LEAudioRouter.csproj
    includes Program.cs
    includes Host/**
    includes Shell/**
    includes Settings/**
    includes Supervision/**
    includes Cli/**

    DOES NOT include Legacy/**
```

This is an architectural invariant, not only a build workaround: archived implementations and their generated `bin/` / `obj/` trees must never become accidental dependencies of the canonical application.

## Reserved next modules

These are architectural slots, not implemented code yet.

| Module | Future responsibility |
|---|---|
| `Lifecycle/` | Windows power and endpoint observations. Observers report facts; they do not perform recovery. |
| `Routing/` | One replaceable audio route generation and its ordered startup/shutdown. |
| `Timing/` | SPSC boundary, occupancy observation, and later clock synchronization/control. |
| `Telemetry/` | Cheap always-on structured runtime observations and worker status. |
| `Diagnostics/` | Explicit experiments such as latency and future drift probes. |

## Dependency direction

```text
Shell ───────► Settings
  │
  └──────────► Supervision

Cli ─────────► future control boundary

Supervision ─► future Lifecycle observations
Supervision ─► future backend worker protocol

future Routing ─► future Timing
future Routing ─► future Telemetry
```

The Shell must never directly own WASAPI clients, ring pointers, capture callbacks, or backend recovery internals.
