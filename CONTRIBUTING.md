# Contributing

LE Audio Router is currently most valuable as a reproducible Windows LE Audio experiment.

Code contributions are welcome, but high-quality hardware/driver test reports are equally important.

## Before opening an issue

Please check:

1. Native Windows LE Audio works without the router.
2. Windows shows **Use LE Audio when available**.
3. The Buds/headset endpoint is actually connected.
4. The Windows default output is a separate physical endpoint.
5. You are testing a known candidate/commit.

If the problem only exists because native Windows LE Audio is unavailable, LE Audio Router cannot fix it at the user-mode routing layer.

## Useful issue template

Please include:

```text
LE Audio Router version / commit:
Windows edition and build:
PC model if relevant:
Bluetooth controller:
Bluetooth driver version:
Audio subsystem / driver if known:
Earbuds / headset:
Device firmware if known:

"Use LE Audio when available" present: yes/no
Windows default output:
Router category:
Spatial Sound:
Reproduction steps:

Observed result:
Expected result:

Does "Restart audio route" fix it:
Does disconnect/reconnect fix it:
Does restarting the router fix it:
Does rebooting Windows fix it:

Relevant lifecycle log:
```

## For stereo-image / hollow-mono reports

This class of report is especially useful because it relates directly to the project's original motivation.

Please describe:

- whether the failure appeared after silence;
- whether it appeared after endpoint reconnect;
- whether it appeared after Windows resume;
- whether mono content sounds centered and solid or hollow/wide;
- whether left/right separation changes between good and bad states;
- whether a fresh worker generation clears it.

Avoid presenting subjective audio impressions as root-cause proof. The important evidence is the reproducible state transition.

## For sleep/resume reports

Include the lifecycle sequence around:

```text
POWER_SUSPEND
POWER_RESUME
WORKER_REPLACE
ENDPOINT_*
WORKER_RUNNING
```

The current invariant is:

> A pre-resume route generation must never be trusted after resume.

## For long-run drift reports

Report:

- approximate run duration;
- workload (music/game/video/mixed);
- whether there were meaningful silence periods;
- audible periodic click/tick;
- obvious latency growth;
- whether a hard route restart changes the behavior.

The current positive-drift guard is provisional. Reports that show artifact frequency or long-run failure are useful design evidence.

## Lifecycle logs and privacy

Persistent logs are intentionally low-volume and should contain lifecycle state rather than captured audio content.

Before posting a log publicly, review it yourself and remove any machine-specific information you do not want to share.

Do not post:

- passwords;
- API keys;
- private file paths unrelated to the project;
- unrelated system logs containing personal information.

## Code contributions

### Architecture constraints

Current hard boundaries:

```text
Tray / Supervisor
Audio Route Worker
```

Do not add another process role without an architecture decision.

Ownership direction:

```text
Shell → Settings / Supervision
Supervision → Lifecycle / worker protocol
Worker Host → Routing
Routing → Timing / Telemetry
Timing → Telemetry
```

Avoid:

- generic Util/Helpers dumping grounds;
- endpoint policy inside audio callbacks;
- WASAPI object teardown from Core Audio notification callbacks;
- route-recovery policy inside Lifecycle observers;
- treating provisional timing mitigation as finished clock synchronization.

### Legacy V0.1

`Legacy/V0.1/` is frozen evidence.

Do not import it as a production library.

If a Round4 implementation needs a behavior from V0.1, re-establish the behavior explicitly in the current architecture rather than coupling production code to the archive.

### Architecture decisions

Read:

- `docs/decisions/0001-out-of-process-route-generation.md`
- `docs/decisions/0002-event-driven-endpoint-reconciliation.md`
- `docs/decisions/0003-power-revision-route-invalidation.md`
- `docs/decisions/0004-provisional-positive-drift-guard.md`

If a change invalidates one of those decisions, update or supersede the ADR rather than silently changing the architecture.

## Build

```powershell
dotnet build .\LEAudioRouter.csproj -c Release
```

## Pull requests

A useful pull request should explain:

- the observed problem;
- why the current owner/module is the right place to change it;
- how the behavior was tested;
- whether it changes lifecycle semantics;
- whether it changes latency/buffering;
- what evidence would falsify the proposed fix.

Small focused changes are preferred over broad rewrites.

## Documentation contributions

Documentation is treated as part of the engineering evidence.

Please preserve the distinction between:

- platform fact;
- project observation;
- inference/hypothesis.

When citing Windows behavior, prefer primary Microsoft documentation.

## License note

The project does not yet have its final public open-source license.

Do not add third-party code with incompatible licensing assumptions before the project license is selected.
