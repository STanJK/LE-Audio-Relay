# Validation

This page is the project's evidence ledger.

The goal is to make it clear which statements are:

- already reproduced;
- currently under daily testing;
- historical observations;
- not yet verified.

The current frozen candidate is:

```text
V0.21 Daily Test Candidate 2
runtime baseline: 1e1e14608afafd7afef2f43084e434b1c67c018a
status: full daily-use validation in progress
```

---

## Status vocabulary

### Validated

A behavior has been deliberately reproduced on the current Round4 path and matched the expected result.

### Daily validation in progress

The mechanism exists and short functional tests may pass, but the project has not yet accumulated enough ordinary multi-hour/multi-day evidence to call it stable.

### Historical observation

Observed in an earlier implementation or test setup and preserved as evidence, but not necessarily reproduced on the current candidate.

### Not claimed

The project does not currently have enough evidence to advertise support.

---

## Current functional validation matrix

| Area | Status | Evidence |
|---|---|---|
| Process Loopback capture excluding worker tree | ✅ Validated | Current route reaches real audio output |
| Persistent destination zero keepalive | ✅ Validated | Original V0.1 daily path and Round4 route |
| 48 kHz / Float32 / stereo route policy | ✅ Validated | Current RouteSession startup |
| GameEffects category | ✅ Validated | Current daily default |
| GameMedia category | ✅ Validated | Manual category-switch test |
| Media category | ✅ Validated | Manual category-switch test |
| Default/unset category | ✅ Validated | Manual category-switch test |
| Manual route restart | ✅ Validated | One worker generation replaced |
| Buds disconnect | ✅ Validated | Worker removed, Tray survives |
| WaitingForEndpoint | ✅ Validated | Zero reconnect worker churn |
| Buds reconnect | ✅ Validated | One fresh generation reaches Running |
| Invalid Windows default output | ✅ Validated | Enters TopologyBlocked |
| Restore safe physical default output | ✅ Validated | Automatically returns to Running |
| Windows suspend/resume | 🧪 In progress | Mechanism implemented; full daily validation ongoing |
| Repeated multi-day power cycles | 🧪 In progress | Candidate purpose |
| Provisional drift guard | 🧪 In progress | Needs long-run and subjective artifact evidence |
| Lifecycle journal usefulness | 🧪 In progress | Needs multi-day evidence |
| Galaxy Buds3 Pro | ✅ Primary validated target | Current daily device |
| Sony LinkBuds S | 🕘 Historical | Earlier project baseline |
| Other LE Audio earbuds/headsets | ⚪ Not claimed | Community testing needed |
| Microphone forwarding | ❌ Out of scope | Deliberately not implemented |
| Full clock synchronization | ❌ Not implemented | Provisional positive-drift guard only |
| Spatial Sound integration | ⚪ Not claimed | Current validation baseline keeps it off |

---

## Endpoint disconnect/reconnect test

Validated flow:

```text
Running
→ disconnect Galaxy Buds3 Pro
→ endpoint re-enumeration reports Absent
→ current worker stops
→ WaitingForEndpoint
→ Backend gen: none
```

The key regression check is:

> No new worker should repeatedly spawn while the target endpoint remains absent.

Reconnect:

```text
Core Audio topology event
→ fresh enumeration
→ target Available
→ start generation N+1
→ worker HELLO
→ real RouteSession startup
→ RUNNING
```

---

## Topology-blocking test

The current route requires the Windows default output to remain a separate physical sink.

Changing the default output to the Buds should produce:

```text
TopologyBlocked
Backend gen: none
```

Restoring the safe physical default output should trigger automatic reconciliation and return to Running.

---

## Category replacement test

Changing:

```text
GameEffects
→ GameMedia
→ Media
→ Default
```

should produce exactly one worker-generation replacement per configuration change.

The Tray process should remain alive.

---

## Power-cycle validation

Power lifecycle is the major V0.21 candidate test.

Expected sleep/resume path:

```text
Running generation N
→ PBT_APMSUSPEND
→ Windows sleep
→ resume
→ PowerRevision increments
→ generation N is stale
→ endpoint reality is re-probed
→ generation N+1 starts if target is eligible
→ Running
```

Current daily-test combinations:

- sleep while Running;
- sleep while WaitingForEndpoint;
- disconnect earbuds while the PC is asleep;
- reconnect after resume;
- repeated sleep/resume cycles;
- category change after resume;
- manual route restart after resume.

The candidate should not be called stable until these have accumulated meaningful ordinary-use evidence.

---

## Drift-guard validation

The provisional drift guard exists to solve one operational problem:

```text
small positive capture/render clock mismatch
→ retained ring fill grows over time
→ eventually ring stays near capacity
```

The guard is considered useful only if it satisfies both:

1. retained queue no longer grows without bound;
2. correction is not subjectively more annoying than the overflow behavior it replaces.

Current control band:

```text
target residual       10 ms
gradual trim stops    11 ms
gradual trim starts   12 ms
hard recenter         20 ms
physical capacity     80 ms
```

Things to watch during daily testing:

- periodic click/tick artifacts;
- sudden timing jump;
- repeated hard recenter;
- RuntimeDroppedAudioFrames increasing despite the guard;
- route latency obviously growing over time.

The project does not yet claim artifact-free clock correction.

---

## Lifecycle-log validation

The log should be useful after days of operation.

Expected low-volume events include:

```text
APP_START
POWER_SUSPEND
POWER_RESUME
ENDPOINT_AVAILABLE
ENDPOINT_ABSENT
TOPOLOGY_BLOCKED
WORKER_RUNNING
WORKER_REPLACE
WORKER_EXIT
MODE_CHANGE
MANUAL_RESTART
```

It should **not** contain:

- heartbeat every second;
- repeated unchanged endpoint state;
- ring occupancy spam;
- overflow warning spam;
- clock-trim warning spam.

---

## Historical V0.1 observations

The frozen V0.1 baseline recorded several useful long-run observations.

### Manual lifecycle recovery

V0.1 had no outer supervisor.

Disconnect/reconnect and Windows suspend/resume required restarting the process manually.

### Overflow semantics were misleading

Very large `OverflowFrames` totals could accumulate without equally obvious audible loss.

The old counter did not distinguish:

- silent zero frames;
- potentially audible audio frames.

Round4 therefore separates silent/audio and intentional correction semantics.

### Rare long-term micro-dropout state

After many days, sleep/wake cycles, and manual route restarts, V0.1 could occasionally develop short periodic micro-dropouts under information-dense audio.

A full Windows reboot restored normal behavior.

This is an observation, not a proven root cause.

One purpose of the V0.21 lifecycle journal is to make recurrence of this state easier to correlate with preceding power and endpoint events.

---

## What would count as strong V0.21 evidence

A reasonable first public baseline would ideally survive:

- several days of ordinary daily use;
- repeated endpoint disconnect/reconnect;
- repeated Windows sleep/resume;
- mixed gaming/media workloads;
- category changes and manual restart;
- no persistent worker churn;
- no systematic ring saturation;
- no new obvious periodic correction artifact;
- useful lifecycle logs when something does fail.

This is an engineering validation target, not a formal certification program.

---

## Reporting a result

When reporting success or failure, include:

- candidate/version;
- Windows build;
- Bluetooth controller;
- Bluetooth driver version;
- headset/earbuds model;
- whether Windows shows **Use LE Audio when available**;
- current render category;
- reproduction sequence;
- relevant lifecycle-log lines.

See [CONTRIBUTING.md](../CONTRIBUTING.md).
