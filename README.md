# LE Audio Router

**Frozen milestone: V0.20.2 — first event-driven endpoint lifecycle release**

V0.20.2 preserves the first Round4 build where the real Process Loopback → Buds route and the Tray/supervisor lifecycle are both in place **and** endpoint disconnect/reconnect is handled by event-driven Windows endpoint reconciliation rather than fixed retry.

## Version lineage

```text
V0.20
first stabilized Tray / Supervisor shell
(no real audio route yet)

V0.20.1
first real audio-routing Tray build
fixed 3-second recovery loop

V0.20.2
real audio-routing Tray build
event-driven endpoint lifecycle
```

## V0.20.2 behavior

V0.20.2 contains:

- one long-lived Windows tray/supervisor process;
- one disposable backend worker process per route generation;
- a real handwritten RouteSession inside the worker;
- Process Loopback capture excluding the worker process tree;
- persistent Buds rendering;
- one SPSC PCM boundary with real-zero keepalive and startup cushion;
- route-local telemetry with separate runtime audio/silent drop counters;
- NAudio/Core Audio endpoint notifications in the Tray/Supervisor process;
- fresh endpoint re-enumeration as the authoritative topology source;
- WaitingForEndpoint with zero worker processes while Buds are absent;
- TopologyBlocked when target matching/default-render policy is invalid;
- bounded fault recovery only when topology remains eligible;
- manual route replacement through **Restart audio route**;
- GameEffects, GameMedia, Media, and Default/unset render-category selection.

## Endpoint lifecycle semantics

```text
notification
    = wake-up only

fresh enumeration
    = authoritative truth

target absent
    => WaitingForEndpoint
    => zero workers
    => zero reconnect spawn attempts

target becomes active
    => create one fresh route generation

route/worker failure while target remains active
    => bounded recovery: 1s, 2s, 5s, 10s, 30s
```

A 30-second safety probe exists only to recover from a missed notification. It re-enumerates topology but does not create a worker while the target remains absent.

## Validation recorded for this release

The V0.20.2 candidate passed the Round4 manual lifecycle checks before release:

- normal connected startup reaches Running;
- disconnect removes the route worker and leaves only the Tray process;
- disconnected state remains quiet without repeated worker PID churn;
- reconnect triggers one fresh generation and returns to Running;
- render-mode change replaces the worker exactly once;
- changing the Windows default output to an invalid topology enters TopologyBlocked with no worker;
- restoring the safe sacrificial default output returns to Running automatically.

## Product semantics

```text
application alive
    = routing intent

endpoint absent
    = wait quietly

endpoint returns
    = recover automatically

user wants routing stopped
    = exit application
```

There is intentionally no separate Router Enabled toggle and no configurable Auto reconnect policy.

See:

- docs/ARCHITECTURE.md
- docs/decisions/0001-out-of-process-route-generation.md
- docs/decisions/0002-event-driven-endpoint-reconciliation.md
- docs/releases/V0.20.2.md
- VF/endpoint-lifecycle.vf.md
- VF/route-session.vf.md
- VF/pcm-relay-boundary.vf.md

## Version metadata

```text
Version              0.20.2
AssemblyVersion      0.20.2.0
FileVersion          0.20.2.0
InformationalVersion V0.20.2
```

## Build

```powershell
dotnet build .\LEAudioRouter.csproj
```

Running the executable without arguments starts the tray shell.
