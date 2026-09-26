# LE Audio Router

**Frozen milestone: V0.20.1 — first real audio-routing Tray build**

V0.20.1 is preserved as a historical Round4 release. It is the first frozen Tray architecture that also contains the rewritten real Process Loopback → Buds audio route.

The earlier V0.20 release remains the first stabilized Tray/supervisor shell before real audio routing was reintroduced.

## V0.20.1 behavior

V0.20.1 contains:

- one long-lived Windows tray/supervisor process;
- one disposable backend worker process per route generation;
- a real handwritten RouteSession inside the worker;
- Process Loopback capture excluding the worker process tree;
- persistent Buds rendering;
- one SPSC PCM boundary with zero keepalive and startup cushion;
- route-local telemetry with separate runtime audio/silent drop counters;
- worker HELLO / RUNNING / heartbeat / FAULTED protocol;
- automatic generation replacement after worker or route failure;
- render-category selection: GameEffects, GameMedia, Media, and Default/unset;
- GameEffects as the default.

## Historical recovery semantics

This release intentionally preserves the recovery behavior that existed before event-driven endpoint lifecycle observation was introduced:

```text
worker / route fails
    ↓
wait fixed 3 seconds
    ↓
spawn a fresh worker generation
    ↓
retry route startup
```

Therefore a physically disconnected Buds endpoint can cause repeated worker creation every ~3 seconds until the endpoint becomes available again.

This is a documented historical behavior of V0.20.1, not the target lifecycle design for later Round4 builds.

See:

- docs/releases/V0.20.1.md
- docs/decisions/0001-out-of-process-route-generation.md
- VF/fixed-retry-recovery.vf.md
- VF/route-session.vf.md
- VF/pcm-relay-boundary.vf.md

## Product semantics

```text
application alive
    = routing intent

route failure
    = automatic replacement after fixed 3-second delay

user wants routing stopped
    = exit application
```

There is intentionally no separate Router Enabled toggle and no configurable Auto reconnect policy.

## Version metadata

```text
Version              0.20.1
AssemblyVersion      0.20.1.0
FileVersion          0.20.1.0
InformationalVersion V0.20.1
```

## Build

```powershell
dotnet build .\LEAudioRouter.csproj
```

Running the executable without arguments starts the tray shell.
