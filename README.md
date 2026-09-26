# LE Audio Router

Round4 is a clean architectural rewrite of the Windows LE Audio relay.

The previous known-good Process Loopback implementation is frozen under [Legacy/V0.1](Legacy/V0.1/README.md). New production code must not depend on the legacy archive.

## Current milestone

The root implementation currently establishes the lifecycle shell before real audio routing is reintroduced:

- one long-lived Windows tray/supervisor process;
- one disposable backend worker process per route generation;
- local named-pipe handshake and heartbeat monitoring;
- automatic worker replacement after failure;
- manual route replacement through **Restart audio route**;
- render-category selection: GameEffects, GameMedia, Media, and Default/unset;
- GameEffects as the default;
- no dependency on NAudio or the Legacy V0.1 implementation yet.

The worker currently exercises lifecycle/IPC only; audio code is the next layer.

## Product semantics

```text
application alive
    = routing intent

worker failure
    = automatic recovery

user wants routing stopped
    = exit application
```

There is intentionally no separate Router Enabled toggle and no configurable Auto reconnect policy.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [ADR 0001](docs/decisions/0001-out-of-process-route-generation.md), and the current [Round4 VF-KB](VF/round4-shell.vf.md).

## Runtime architecture

```mermaid
flowchart TD
    Host["Process Host"] --> Shell["Tray / Supervisor"]
    Host --> CLI["CLI Adapter"]
    Host --> Worker["Audio Route Worker mode"]

    Shell --> Config["Router Configuration"]
    Shell --> Supervisor["Backend Supervisor"]
    Supervisor -->|"spawn / replace"| Worker
    Worker -->|"heartbeat / status"| Supervisor

    Worker -. next milestone .-> Route["Audio Route Generation"]
```

### Hard invariants

1. **Application lifetime is owned by the tray/supervisor process.**
2. **Application alive implies routing intent.**
3. **Recovery is automatic and not user-configurable.**
4. **One worker PID represents one disposable Audio Route generation.**
5. **The process boundary is a user-mode fault/diagnostic boundary, not an audio-domain boundary.**
6. **Exactly two runtime process roles are allowed unless a later ADR changes this.**
7. **Legacy V0.1 is evidence, not a library.**
8. **Clock synchronization remains a future Timing module.**

## Build

```powershell
dotnet build .\LEAudioRouter.csproj
```

Running the executable without arguments starts the tray shell.
