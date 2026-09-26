# ADR 0003 — Power revision invalidates pre-resume route generations

**Status:** Accepted  
**Date:** 2026-09-27

## Context

Endpoint disconnect/reconnect is already reconciled from Core Audio topology. Daily use also includes frequent Windows sleep/resume cycles.

A WASAPI/Process Loopback route created before suspend should not be assumed trustworthy after resume, even if the same endpoint appears Active again.

## Decision

Observe suspend/resume directly through WM_POWERBROADCAST in the long-lived Tray process.

The PowerObserver callback only updates an in-memory PowerSnapshot and wakes supervision.

It does not:

- enumerate audio endpoints;
- dispose or create WASAPI objects;
- stop/start a worker;
- write persistent logs.

The snapshot contains:

```text
IsSuspended
PowerRevision
SuspendCount
```

On each resume epoch, PowerRevision increases.

Every WorkerGeneration stores the PowerRevision under which it was created.

After resume:

```text
worker.PowerRevision != current.PowerRevision
    => worker is stale
    => replace the complete generation
```

While IsSuspended is true, supervision does not create a new worker.

## Rationale

The rule reuses the existing generation-replacement boundary instead of teaching every WASAPI object how to survive sleep.

It also keeps PBT_APMSUSPEND handling nonblocking and very small.

## Consequences

- no audio generation is trusted across suspend/resume;
- endpoint reality is re-probed after resume;
- Buds absent after wake naturally becomes WaitingForEndpoint;
- route startup failure after wake naturally uses the existing bounded recovery path;
- no separate sleep-specific retry loop exists.
