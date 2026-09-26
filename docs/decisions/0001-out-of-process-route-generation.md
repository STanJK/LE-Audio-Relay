# ADR 0001 — Keep one out-of-process Audio Route generation

**Status:** Accepted  
**Date:** 2026-09-27

## Context

Round4 separates the long-lived Windows tray/supervisor lifetime from the shorter-lived lifetime of one audio route generation.

Two hosting models were considered:

1. keep the route generation in the tray process and rebuild it as objects/tasks;
2. host the route generation in one disposable child process and replace that process when the route is rebuilt.

A single-process design is more conventional and avoids one PID plus local IPC. Ordinary WASAPI errors such as endpoint invalidation do not themselves require process isolation.

However, this project deliberately explores unstable lifecycle boundaries across Windows CoreAudio/WASAPI, LE Audio endpoints, Bluetooth/audio drivers, repeated suspend/resume, and later deeper controller/WinUSB work. Long-run debugging benefits from being able to replace all process-local route state at once.

## Decision

Keep **one** out-of-process Audio Route Worker.

The tray/supervisor process owns product lifetime and recovery policy. One child process owns one route generation.

The child is spawned from the same executable using `--backend-worker`; this is a process role, not a second product binary.

Recovery is automatic and non-configurable.

The user does not receive separate "Router Enabled" or "Auto reconnect" controls.

## Rationale

The process boundary provides three concrete properties:

### 1. Fault containment

A worker-local unhandled exception, detectable hang, or user-mode native/interop failure can terminate or invalidate the worker without necessarily terminating the tray shell.

### 2. Complete route teardown

Replacing a generation guarantees replacement of worker-local threads, handles, COM/WASAPI objects, managed state, and process-local interop state rather than relying on every route object to clean itself up perfectly.

### 3. Diagnostic discrimination

A fresh worker PID is an experimental boundary.

If a fault disappears after worker replacement, process-local state becomes a plausible contributor.

If the fault survives worker replacement, evidence points toward state below the worker boundary, such as Windows Audio services, Bluetooth stack/driver/controller state, or the earbuds.

## Non-goals

This boundary is not intended to:

- isolate kernel bugchecks;
- protect against system-wide driver crashes;
- turn the application into microservices;
- place capture, render, timing, or telemetry in separate processes;
- justify permanent complexity if operational evidence later shows no value.

## Constraints

Exactly two runtime process roles are permitted by this decision:

```text
Tray / Supervisor
Audio Route Worker
```

Any additional process role requires a separate architecture decision.

The worker protocol must remain local, typed, and minimal.

## Reconsideration triggers

Revisit this decision if long-run evidence shows that:

- process replacement never provides recovery or diagnostic value;
- IPC/process lifecycle complexity materially harms maintainability or latency;
- an in-process RouteHost can provide equivalent containment for observed failures;
- a future own-stack architecture requires a different isolation boundary.

The current decision optimizes for recoverability and debugging while the hardware/driver interaction surface is still evolving.
