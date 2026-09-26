# Project history

LE Audio Relay did not start as a general audio-router project.

It started as a sequence of experiments around one Windows LE Audio lifecycle problem and gradually became a supervised daily-use tool.

---

## Phase 1 — Native Windows LE Audio investigation

The project initially investigated why apparently capable Bluetooth hardware could behave differently across Windows and Linux.

Work included:

- Intel AX211 / AX210 experiments;
- Intel SST / Bluetooth LE Audio driver packages;
- Windows audio kernel/proxy components;
- vendor-specific LE Audio integration;
- comparison with Linux LE Audio behavior;
- later use of additional controllers as reference points.

This phase established an important practical reality:

> "The controller supports Bluetooth LE" is not enough to explain whether Windows exposes a working LE Audio path.

The Windows integration depends on the larger OEM / Bluetooth / audio-driver stack.

The project eventually stopped trying to reverse the private vendor path as the main daily solution.

---

## Phase 2 — Earbud lifecycle behavior

Two devices were especially useful during early testing:

- Sony LinkBuds S
- Samsung Galaxy Buds3 Pro

Observed issues included:

- start-of-stream delay/truncation after silence;
- destination teardown during silence;
- inconsistent stereo presentation after route reconstruction;
- hollow mono presentation during the abnormal stereo state.

Galaxy Buds3 Pro improved some silence behavior compared with LinkBuds S, but the route-rebuild state remained important enough to investigate.

The project still does not claim a proven root cause for that state.

---

## V0.1 — Process Loopback Keepalive Relay

V0.1 established the first known-good daily architecture.

```text
Windows apps
→ sacrificial physical render sink
→ Process Loopback
→ SPSC ring
→ persistent Buds render
```

Key result:

> Keeping the final destination render stream alive and returning real zero PCM during silence prevented the persistent abnormal soundstage from reproducing in the normal daily path.

V0.1 also established:

- 48 kHz Float32 stereo;
- destination-first startup;
- KEEPALIVE → ARMED → RELAY;
- a 10 ms target cushion;
- an 80 ms physical ring capacity;
- a hot latency diagnostic path.

Frozen source:

- `Legacy/V0.1/`

### What V0.1 did not solve

V0.1 had one process invocation for one route lifetime.

Therefore:

- disconnect/reconnect required manual restart;
- suspend/resume required manual restart;
- health observation did not rebuild the route;
- old overflow telemetry mixed audible and silent frame loss;
- long runs could accumulate clock drift.

Those limitations motivated Round4.

---

## Round4 — Product architecture rewrite

Round4 changed the unit of design.

Instead of:

```text
one process = one route
```

it introduced:

```text
long-lived product lifetime
+
replaceable route generation
```

The user-facing model became:

```text
application alive
    = routing intent
```

No Router Enabled toggle.

No user-configurable Auto reconnect switch.

---

## V0.20 — First stabilized Tray form

V0.20 froze the first Tray/Supervisor architecture before real audio was reintroduced.

It established:

- persistent tray lifetime;
- worker-generation concept;
- local worker protocol;
- heartbeat monitoring;
- category selection;
- manual route restart;
- the two-process diagnostic/fault boundary.

At this point, the Tray shape was stable enough to freeze independently of the audio implementation.

---

## V0.20.1 — First real Round4 audio route

V0.20.1 connected the rewritten real audio route inside the disposable worker.

It added:

- Process Loopback source;
- persistent destination render;
- PCM relay boundary;
- route telemetry;
- real RUNNING handshake semantics.

Recovery was still simple:

```text
failure
→ wait 3 seconds
→ spawn another worker
```

That recovered transient failures, but it also spawned workers repeatedly while the earbuds were physically absent.

V0.20.1 is preserved because that behavior is useful historical evidence.

---

## V0.20.2 — Event-driven endpoint lifecycle

V0.20.2 replaced fixed reconnect retry with endpoint-aware reconciliation.

Key rule:

```text
notification
    = wake-up

enumeration
    = truth
```

When the target is absent:

```text
WaitingForEndpoint
zero workers
```

When it returns:

```text
one fresh generation
→ Running
```

Manual testing validated:

- disconnect without worker churn;
- reconnect;
- category replacement;
- TopologyBlocked;
- recovery when the safe default output returns.

---

## V0.21 Daily Test Candidate 1

The current candidate adds the pieces needed for sustained ordinary use.

### Power lifecycle

Windows suspend/resume is observed through `WM_POWERBROADCAST`.

Resume increments a PowerRevision.

Any worker created under an older power revision is replaced.

### Lifecycle journal

The Tray records low-volume lifecycle evidence under:

```text
%LOCALAPPDATA%\LEAudioRelay\logs\
```

The journal intentionally avoids high-frequency warning spam.

### Provisional drift guard

The route now protects the 10 ms retained cushion from slow positive producer/consumer clock drift.

Current post-render control band:

```text
target residual       10 ms
gradual trim stops    11 ms
gradual trim starts   12 ms
hard recenter         20 ms
physical capacity     80 ms
```

This is explicitly temporary mitigation, not final clock synchronization.

### Current status

V0.21 Daily Test Candidate 1 is frozen while full daily-use validation proceeds.

It is not yet the formal stable V0.21 release.

---

## Future direction

Near-term public-release work:

- finish daily validation;
- package a reproducible build;
- choose an open-source license;
- polish user-facing diagnostics;
- publish the Windows LE Audio findings and reproduction story.

Longer-term technical work may include:

- proper clock-drift estimation and control;
- safer continuous correction;
- broader LE Audio device validation;
- deeper Windows/Bluetooth diagnostics;
- experimental own-stack/dongle work as a separate track.

The relay should remain a focused, comprehensible tool even if those experiments grow.
