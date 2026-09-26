# Documentation

LE Audio Relay documentation is split by audience and question rather than by source-code folder.

## Start here

| Document | Read this when you want to... |
|---|---|
| [Getting started](GETTING_STARTED.md) | Build the current candidate and run it correctly |
| [Why this exists](WHY_THIS_EXISTS.md) | Understand the Windows LE Audio problem this project is exploring |
| [How it works](HOW_IT_WORKS.md) | Understand the Process Loopback → persistent render design |
| [Validation](VALIDATION.md) | See what is verified, under test, or still unknown |
| [Troubleshooting](TROUBLESHOOTING.md) | Diagnose startup, endpoint, reconnect, or power-cycle problems |
| [Architecture](ARCHITECTURE.md) | Inspect ownership boundaries, process roles, and recovery semantics |
| [Project history](PROJECT_HISTORY.md) | Follow the evolution from V0.1 to the V0.21 daily-test candidate |
| [Community and Microsoft publishing](COMMUNITY_AND_MICROSOFT.md) | Reuse the evidence accurately in articles, reports, and Microsoft channels |
| [Public release checklist](PUBLIC_RELEASE_CHECKLIST.md) | Track what must be finished before formal V0.21 |

## Architecture decisions

The project records decisions that materially constrain future implementation:

- [ADR 0001 — Keep one out-of-process Audio Route generation](decisions/0001-out-of-process-route-generation.md)
- [ADR 0002 — Event-driven endpoint reconciliation](decisions/0002-event-driven-endpoint-reconciliation.md)
- [ADR 0003 — Power revision invalidates pre-resume route generations](decisions/0003-power-revision-route-invalidation.md)
- [ADR 0004 — Temporary positive-drift guard for daily-use validation](decisions/0004-provisional-positive-drift-guard.md)
- [ADR 0005 — Product identity is LE Audio Relay](decisions/0005-product-identity-le-audio-relay.md)

## Evidence discipline

Project documentation distinguishes three kinds of statements:

1. **Platform fact** — supported by Microsoft or another primary technical source.
2. **Project observation** — reproduced on this project's test systems.
3. **Inference or design hypothesis** — a plausible explanation that has not been proven as root cause.

That distinction is deliberate. LE Audio behavior depends on the complete Windows, Bluetooth-controller, audio-driver, and device stack, so a local observation should not be generalized into a platform-wide claim without evidence.

## Current release status

The current public-facing baseline is:

```text
V0.21 Daily Test Candidate 1
status: frozen candidate
validation: in progress
stable/public V0.21: not yet declared
```

See [Validation](VALIDATION.md) for the current evidence matrix.
