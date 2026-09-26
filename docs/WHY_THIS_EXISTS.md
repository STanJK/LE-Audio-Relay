# Why this exists

LE Audio Router started as a practical response to one repeatable Windows Bluetooth LE Audio failure mode.

It is useful to separate the story into two parts:

1. what Microsoft publicly documents about Windows LE Audio;
2. what this project observed on real test systems.

They are not the same kind of evidence.

---

## What Microsoft documents about Windows LE Audio

Bluetooth LE Audio is a native Windows 11 feature.

Microsoft documents LE Audio support as beginning with Windows 11 version 22H2, and its driver architecture depends on more than the Bluetooth version alone.

A Windows PC needs:

- Windows 11 version 22H2 or newer;
- compatible Bluetooth LE capability;
- compatible audio codec support;
- LE Audio capable Bluetooth and audio drivers from the PC manufacturer.

Microsoft's user-facing support check is the presence of:

**Settings → Bluetooth & devices → Devices → Use LE Audio when available**

If that setting is absent, the PC does not currently have complete LE Audio support from Windows' point of view.

Reference:

- https://support.microsoft.com/en-us/windows/hardware/bluetooth/check-if-a-windows-11-device-supports-bluetooth-low-energy-audio

### Why the driver stack matters

Microsoft's Windows driver documentation describes LE Audio using a vendor-specific audio path (VSAP) and Windows Audio Class Extensions (ACX).

The documentation also explains that, as of the Bluetooth Core 5.3 architecture discussed there, there is no standard HCI path for the host platform to send and receive isochronous audio data to and from the controller. Windows therefore defines a vendor-specific integration path for LE Audio streaming.

Reference:

- https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/bluetooth-low-energy-audio

The practical consequence is important:

> LE Audio support is a property of the complete platform integration, not merely "the Bluetooth adapter supports Bluetooth 5.x."

That makes controller, OEM driver, audio-driver, Windows-build, and endpoint lifecycle behavior relevant when debugging.

---

## What this project observed

The observations below are **project observations**, not Microsoft claims and not proof that every Windows LE Audio system behaves the same way.

### Initial symptoms

Across the project's early daily tests, two LE Audio earbud generations were especially informative:

- Sony LinkBuds S
- Samsung Galaxy Buds3 Pro

The project observed behaviors including:

- application audio being delayed or truncated at stream start after silence;
- LE Audio render paths being torn down after periods of silence;
- after route reconstruction, Galaxy Buds3 Pro sometimes returning with an abnormal stereo image;
- mono material sounding unusually hollow during that abnormal state;
- reconnecting or rebuilding the route restoring the expected presentation;
- some failures self-recovering after a short interval, while others could persist until reconnect.

The project does **not** currently claim a proven root cause for the stereo-state fault.

The important reproducible correlation was lifecycle:

```text
silence
→ destination stream disappears / rebuilds
→ sometimes normal
→ sometimes abnormal
```

### Why simple Windows "Listen" routing was not enough

An early routing path used normal Windows audio plumbing.

It could reproduce the same bad state and, in some cases, appeared to preserve the abnormal route until the endpoint was reconnected.

That pushed the project away from "find another mixer checkbox" and toward explicit ownership of the destination AudioClient lifetime.

---

## The key experiment

The breakthrough experiment was deliberately simple:

> Keep the final Buds render stream alive continuously.

The route starts the destination first and never treats ordinary silence as a reason to stop rendering.

When there is no captured application audio, the destination still receives complete buffers containing **real zero PCM**.

With that behavior, the persistent abnormal soundstage stopped reproducing in the daily path.

This did not prove which layer originally created the fault.

It did prove something operationally useful:

```text
allow destination teardown/rebuild
    = unstable on our test path

keep one destination render generation alive
    = materially more stable
```

That became the core invariant of the project.

---

## Why Process Loopback

The next problem was how to keep the Buds render stream alive while still letting ordinary Windows applications behave normally.

Windows provides Process Loopback capture that can include or exclude a target process tree.

LE Audio Router uses:

```text
PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE
```

This allows the worker to capture ordinary render audio while excluding its own destination render stream.

Microsoft reference:

- https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ne-audioclientactivationparams-process_loopback_mode
- https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/

The resulting route is:

```text
ordinary applications
→ sacrificial physical Windows output
→ Process Loopback capture
→ relay buffer
→ persistent LE Audio render
```

The sacrificial sink is intentional. It decouples application render lifetime from LE Audio destination lifetime.

---

## Why this became more than a one-file workaround

The original V0.1 relay proved the keepalive idea, but daily operation exposed lifecycle problems around it:

- unplug/reconnect required manual restart;
- Windows sleep/resume required manual restart;
- raw overflow counters mixed silent frames with audible data;
- long runs exposed clock mismatch between capture and render;
- rare long-term micro-dropouts could appear after many days and power cycles.

Round4 therefore treats the relay as a product lifecycle problem, not just an audio callback problem.

Current architecture adds:

- a persistent tray/supervisor lifetime;
- a disposable route-worker generation;
- event-driven endpoint observation;
- authoritative endpoint re-enumeration;
- suspend/resume power epochs;
- low-volume lifecycle evidence;
- provisional positive-drift mitigation.

---

## What the project is trying to learn

The immediate practical goal is simple:

> Make Windows LE Audio usable enough to leave running every day.

The engineering questions are broader:

- Which failures are endpoint lifecycle failures versus audio-buffer failures?
- Which failures disappear when all user-mode route state is replaced?
- Which failures survive a fresh worker process and therefore point lower in the Windows/Bluetooth stack?
- How stable is Windows LE Audio across repeated sleep/resume and reconnect cycles?
- How much explicit application-level clock control is required for long-running Process Loopback relay paths?

Those questions are useful even if a future Windows/driver update removes the original Buds-specific failure.

---

## What this project does not claim

This project does not claim that:

- every Windows LE Audio PC has the same bug;
- Galaxy Buds3 Pro firmware is the root cause;
- Intel, MediaTek, Qualcomm, or Microsoft has been proven responsible;
- the current provisional drift guard is a final synchronization design;
- keeping an AudioClient open is the correct fix for every LE Audio device.

The repository is intended to preserve reproducible observations, implementation evidence, and architecture decisions so those questions can be discussed with less guesswork.

---

## Relevant platform references

- Microsoft Support — Check LE Audio capability  
  https://support.microsoft.com/en-us/windows/hardware/bluetooth/check-if-a-windows-11-device-supports-bluetooth-low-energy-audio

- Microsoft Learn — Bluetooth Low Energy (LE) Audio architecture  
  https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/bluetooth-low-energy-audio

- Microsoft Learn — Process Loopback mode  
  https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ne-audioclientactivationparams-process_loopback_mode

- Microsoft Learn — Application loopback sample  
  https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/

Next: [How it works](HOW_IT_WORKS.md)
