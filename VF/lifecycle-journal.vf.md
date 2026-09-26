<!--vf:node
schema "vf-kb/0.1"
id "leaudio-router.round4-shell.lifecycle-journal"
kind "behavior"
parent "leaudio-router.round4-shell"
coverage "mapped"
-->

<!--vf:summary
entry "The Tray/Supervisor observes a low-frequency lifecycle transition."
problem "Long daily runs need durable evidence of power, endpoint, worker, and user-control transitions without turning high-frequency audio diagnostics into log spam."
behavior "Append one sanitized timestamped line to a daily file under LocalApplicationData for application, power, endpoint, worker-generation, mode, and manual-restart transitions only."
exit "Lifecycle history survives process restarts while heartbeat, ring warnings, drift warnings, and per-second telemetry remain absent from persistent logs."
-->

<!--vf:source
id "logger"
repo "STanJK/le-audio-windows-relay"
rev "e7965f4b1fc34dcbf28d1f3e86406ef4b58e2df2"
path "Telemetry/LifecycleEventLog.cs"
symbol "LifecycleEventLog"
-->

<!--vf:source
id "supervisor"
repo "STanJK/le-audio-windows-relay"
rev "e7965f4b1fc34dcbf28d1f3e86406ef4b58e2df2"
path "Supervision/BackendSupervisor.cs"
symbol "BackendSupervisor"
-->

<!--vf:source
id "shell"
repo "STanJK/le-audio-windows-relay"
rev "e7965f4b1fc34dcbf28d1f3e86406ef4b58e2df2"
path "Shell/TrayApplicationContext.cs"
symbol "TrayApplicationContext"
-->

<!--vf:claim
id "journal-is-lifecycle-only"
type "fact"
text "The persistent journal is written only from application/supervision lifecycle transitions and is not called from the worker heartbeat or PCM warning path."
evidence "logger"
evidence "supervisor"
evidence "shell"
-->

<!--vf:claim
id "journal-is-daily-localappdata"
type "fact"
text "LifecycleEventLog appends daily UTF-8 files under LocalApplicationData/LEAudioRouter/logs and catches its own I/O failures so logging cannot break routing."
evidence "logger"
-->

**Why:** Long-run validation requires historical lifecycle evidence, but noisy ring/heartbeat logs would obscure the events needed to correlate regressions. [explain →](./round4-shell.fact.md#journal-why)

**What:** A best-effort daily local journal records only low-frequency application, power, endpoint, worker, mode, and restart transitions. [explain →](./round4-shell.fact.md#journal-what)

**Outcome:** Multi-day dogfooding can be reconstructed without accumulating warning spam or making logging part of the audio critical path. [explain →](./round4-shell.fact.md#journal-outcome)

```text
APP_START / APP_EXIT
POWER_SUSPEND / POWER_RESUME
ENDPOINT_AVAILABLE / ENDPOINT_ABSENT / ENDPOINT_AMBIGUOUS
TOPOLOGY_BLOCKED
WORKER_RUNNING / WORKER_STOP / WORKER_EXIT / WORKER_REPLACE
MODE_CHANGE
MANUAL_RESTART

NOT PERSISTED:
heartbeat
ring warnings
overflow warnings
clock-trim warnings
per-second telemetry
```
