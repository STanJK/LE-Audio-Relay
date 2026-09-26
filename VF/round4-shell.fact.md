# Round4 Shell and Worker Architecture — Fact Page

This page is the human explanation layer for the current Round4 VF-KB nodes. Canonical machine semantics remain in the sibling `.vf.md` files.

## Root module

### root-why

V0.1 coupled application lifetime directly to one audio-route lifetime. Round4 introduces a durable Windows tray/supervisor lifetime so route generations may be destroyed and recreated without requiring the user to restart the product manually. The user-facing model is deliberately smaller than the internal lifecycle model: opening the application already expresses the intent to route audio, so separate Enabled and Auto reconnect controls were removed rather than preserved as redundant configuration.

### root-what

The normal executable entry starts a WinForms tray application. The tray exposes render-category selection, manual route restart, and Exit. The supervisor runs behind that UI and owns replacement of the current backend generation. Recovery policy is automatic. The tray does not own WASAPI clients, capture callbacks, ring state, or route-local timing behavior.

### root-outcome

The tray remains the durable product lifetime. While it is alive, supervision attempts to maintain one current route generation. Exiting the application is the canonical way to stop routing and terminate supervision.

## Worker generation

### worker-why

The project intentionally retains one child-process boundary even though ordinary WASAPI invalidation can be handled in-process. The reason is not that a child process can protect against kernel or hardware crashes; it cannot. The boundary is useful because it guarantees replacement of process-local managed state, worker threads, handles, COM/WASAPI objects, and interop state. It also creates a debugging experiment: faults cleared by a fresh worker PID implicate process-local state more strongly, while faults that survive replacement point below that boundary.

### worker-what

The supervisor assigns a generation number, creates a private named pipe, and spawns the same executable in `--backend-worker` mode with an immutable configuration snapshot. The worker connects, sends a typed HELLO/RUNNING sequence, then emits heartbeat messages. The supervisor replaces the generation after explicit restart intent, configuration drift, worker completion, pipe failure, or heartbeat timeout.

The process split occurs exactly once. Capture, render, timing, telemetry, and route coordination are expected to remain inside the worker rather than becoming their own processes.

### worker-outcome

A healthy generation stays current. A stale or failed generation is stopped and disposed, and the supervisor automatically proceeds toward a fresh generation while the tray product lifetime remains alive. The process boundary remains intentionally replaceable as an architectural choice if later operational evidence shows its cost exceeds its recovery/debug value.
