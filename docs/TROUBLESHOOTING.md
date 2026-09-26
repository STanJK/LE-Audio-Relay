# Troubleshooting

This guide covers the current Round4 / V0.21 daily-test architecture.

Start with the simplest rule:

> First make native Windows LE Audio work. Then debug LE Audio Relay.

---

## The Tray says WaitingForEndpoint

Meaning:

```text
the configured target render endpoint is not currently Active
```

Check:

1. Are the earbuds connected in Windows?
2. Does Windows show the expected audio endpoint?
3. Is LE Audio actually enabled?
4. Does **Use LE Audio when available** exist and remain enabled?
5. Can Windows play directly to the device without the router?

Expected process state while waiting:

```text
Tray process: yes
Worker process: no
```

This is intentional.

Do not expect a worker to retry every few seconds.

---

## The Tray says TopologyBlocked

The current daily route refuses to run when the Windows default render topology is unsafe.

Common causes:

- Buds are the Windows default output;
- CABLE Input is the Windows default output;
- no multimedia default render endpoint exists;
- multiple active endpoints match the configured destination name.

Fix:

1. Set Windows default output to a separate physical sink.
2. Keep the Buds connected but not default.
3. Wait for automatic reconciliation.

Expected:

```text
TopologyBlocked
→ change default output
→ Starting
→ Running
```

---

## Audio is playing from the sacrificial sink instead of the Buds

The sacrificial endpoint is supposed to receive the application's Windows render stream.

LE Audio Relay should simultaneously capture that mix and forward it to the Buds.

If you hear only the sacrificial sink:

1. confirm the Tray says Running;
2. confirm a `--backend-worker` process exists;
3. confirm the Buds endpoint is Active;
4. use **Restart audio route**;
5. inspect the lifecycle log for worker exit/recovery.

If the sacrificial sink is physically audible and distracting, use a stable output you can mute externally without invalidating the endpoint.

---

## The route never reaches Running

Check the lifecycle log.

Common startup blockers:

### Destination not found

The configured name currently expects:

```text
Galaxy Buds3 Pro
```

If Windows exposes a different friendly name, the resolver may not match it.

### Multiple matching destinations

The current resolver requires exactly one active matching render endpoint.

### Destination format mismatch

The current route expects:

```text
48 kHz
Float32
stereo
```

The current implementation deliberately fails instead of silently inserting a resampler.

### Unsafe Windows default output

Do not use:

- Buds;
- CABLE Input.

---

## Disconnect causes repeated worker processes

That is a regression.

The current endpoint-lifecycle design should do:

```text
disconnect
→ WaitingForEndpoint
→ zero workers
```

Check processes:

```powershell
Get-CimInstance Win32_Process -Filter "Name='LEAudioRelay.exe'" |
    Select-Object ProcessId,ParentProcessId,CommandLine
```

If workers keep appearing while the endpoint is absent, report:

- exact candidate/commit;
- lifecycle log;
- Windows build;
- endpoint sequence.

V0.20.1 intentionally had a fixed 3-second retry loop. V0.20.2 and later should not.

---

## Resume does not restore audio

Expected behavior:

```text
resume
→ PowerRevision changes
→ pre-resume worker becomes stale
→ endpoint re-probe
→ fresh generation
```

Check the log for:

```text
POWER_SUSPEND
POWER_RESUME
WORKER_REPLACE ... reason=power_resume
WORKER_RUNNING
```

If the Buds are not Active after resume, the correct state is:

```text
WaitingForEndpoint
```

The router should wait for Windows to expose the endpoint again.

---

## Audio becomes wide, separated, or hollow on mono material

This symptom is part of the original project motivation.

If it occurs on the current persistent-route path:

1. record whether it happened after silence, reconnect, resume, or manual restart;
2. note whether **Restart audio route** clears it;
3. note whether disconnect/reconnect clears it;
4. capture the lifecycle log around the event;
5. report the headset model and controller/driver information.

Do not assume the project already knows the root cause.

A fresh report that reproduces this on the current Round4 path is especially valuable.

---

## Periodic click or tick during long continuous audio

This may be relevant to the provisional drift guard.

The current guard can intentionally slip one complete stereo sample frame while reducing positive queue drift.

Things to record:

- how often the click occurs;
- whether it occurs only during uninterrupted audio;
- whether it stops after silence;
- whether restarting the route changes the behavior;
- whether it appears before or after sleep/resume.

The current guard is temporary and does not claim perceptually transparent clock correction.

---

## Latency seems to grow over time

That would indicate retained queue growth is not being controlled as intended.

The normal retained target is:

```text
~10 ms post-render residual cushion
```

The 80 ms ring capacity is not the target latency.

Report long-run latency growth if it is clearly reproducible.

---

## There is no log file

Expected directory:

```text
%LOCALAPPDATA%\LEAudioRelay\logs\
```

Open:

```powershell
explorer "$env:LOCALAPPDATA\LEAudioRelay\logs"
```

Logging is best-effort and must not break routing. An I/O failure can therefore prevent log output without crashing the router.

---

## The log is too noisy

That is also a regression.

Persistent logs should not contain:

- one-second heartbeats;
- ring warnings;
- overflow warnings;
- clock-trim warnings;
- unchanged endpoint state every safety probe.

Report any persistent-log spam with a short excerpt.

---

## The PC does not show "Use LE Audio when available"

According to Microsoft, the PC does not currently have complete LE Audio support available to Windows.

Possible reasons include:

- hardware limitation;
- missing/incompatible Bluetooth driver;
- missing/incompatible audio subsystem driver.

Reference:

https://support.microsoft.com/en-us/windows/hardware/bluetooth/check-if-a-windows-11-device-supports-bluetooth-low-energy-audio

LE Audio Relay cannot work around missing platform LE Audio support.

---

## Spatial Sound

The current validation baseline recommends Spatial Sound off.

If you test Spatial Sound:

- report it explicitly;
- do not compare results as though they came from the current baseline.

---

## Reset sequence for a badly confused state

Use the smallest reset that works:

1. **Restart audio route**
2. disconnect/reconnect the Buds
3. exit/restart LE Audio Relay
4. toggle/re-establish the Windows audio endpoint
5. reboot Windows

The point of the worker boundary is partly diagnostic:

- if worker replacement fixes it, process-local state is implicated;
- if the issue survives replacement, the state may be below the worker process.

That is evidence, not proof of root cause.

---

## Before opening an issue

Collect:

```text
Windows build:
Bluetooth controller:
Bluetooth driver:
Earbuds/headset:
Firmware if known:
LE Audio setting present:
Router candidate/version:
Render category:
Default Windows output:
Reproduction steps:
Does Restart audio route fix it:
Does reconnect fix it:
Does Windows reboot fix it:
Relevant lifecycle log:
```

See [CONTRIBUTING.md](../CONTRIBUTING.md).
