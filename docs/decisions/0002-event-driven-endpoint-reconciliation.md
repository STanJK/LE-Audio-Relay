# ADR 0002 — Event-driven endpoint reconciliation

**Status:** Accepted  
**Date:** 2026-09-27

## Context

The first real Round4 route used a simple recovery loop: when a worker or route failed, the supervisor waited a fixed interval and spawned another generation.

That behavior recovered successfully, but it conflated two different situations:

1. the target Buds endpoint is physically/logically absent;
2. the endpoint is present but route initialization or runtime failed.

When the earbuds were disconnected, fixed retry repeatedly created short-lived worker PIDs and repeatedly touched WASAPI even though Windows topology had not changed.

## Decision

Use Windows Core Audio endpoint notifications as **wake-up signals** for supervisor reconciliation.

Use fresh endpoint enumeration as the **authoritative source of truth**.

The lifecycle flow is:

```text
Core Audio notification
    => nonblocking wake signal
    => supervisor re-enumerates endpoint reality
    => policy decides whether a worker should exist
```

The NAudio notification client is created with:

```text
useSynchronizationContext: false
```

so callbacks arrive directly on the Windows audio worker thread. Therefore notification handlers are constrained to coalescing a wake signal and returning immediately.

They must not:

- enumerate endpoints;
- create or dispose WASAPI objects;
- start or stop workers;
- run recovery policy;
- block waiting for the supervisor.

## Reconciliation rules

### Target absent

```text
target endpoint absent
    => stop any stale worker
    => WaitingForEndpoint
    => do not spawn replacement workers
```

Reconnect is silent and automatic: when endpoint topology changes, the observer wakes the supervisor, the supervisor re-probes, and a new route generation is created only after the target is again active.

### Topology blocked

If more than one active endpoint matches the configured target, or the Windows default multimedia render endpoint points to Buds/CABLE rather than a sacrificial sink:

```text
=> TopologyBlocked
=> zero workers
=> wait for topology change
```

### Route fault while endpoint remains eligible

A worker/route failure with an active target and safe default route remains a real recovery failure. It uses bounded backoff:

```text
1 s
2 s
5 s
10 s
30 s
30 s ...
```

This backoff is distinct from endpoint reconnect behavior.

## Notification versus truth

Notifications are intentionally not interpreted as authoritative state.

Bluetooth/LE Audio reconnect can produce multiple add/state/default notifications while Windows reconstructs endpoint topology. Endpoint IDs and ordering are not treated as stable lifecycle semantics.

Therefore:

```text
notification = something changed
enumeration  = what is true now
```

## Safety probe

A 30-second low-frequency endpoint probe remains active while waiting or blocked. Its sole purpose is to recover if a Core Audio notification is missed.

It does not spawn a worker unless the authoritative probe says the target is active and topology is eligible.

## Consequences

Benefits:

- disconnected earbuds produce zero worker churn;
- reconnect is event-driven and silent;
- fewer unnecessary WASAPI initialization attempts;
- the supervisor state reflects reality rather than retry activity;
- endpoint lifecycle and route fault recovery become separate concepts.

Costs:

- the tray/supervisor process now depends on NAudio/Core Audio notification support;
- lifecycle state becomes slightly richer;
- a safety probe remains necessary for defensive robustness.

## Reconsideration triggers

Revisit this decision if:

- endpoint notifications prove unreliable on a target Windows/LE Audio stack;
- Bluetooth endpoint state remains Active while physically disconnected often enough that additional transport-level observation is required;
- suspend/resume requires a stronger power-session boundary than endpoint notifications provide.
