# LE Audio Relay

**A Windows tray relay for persistent Bluetooth LE Audio playback with comparatively low added latency, automatic reconnect, and sleep/resume recovery.**

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
![Status](https://img.shields.io/badge/status-V0.21%20daily%20test-orange)
![Audio](https://img.shields.io/badge/audio-Bluetooth%20LE%20Audio-0A66C2)

[中文](README.zh-CN.md) · [Getting started](docs/GETTING_STARTED.md) · [Why this exists](docs/WHY_THIS_EXISTS.md) · [How it works](docs/HOW_IT_WORKS.md) · [Validation](docs/VALIDATION.md) · [Troubleshooting](docs/TROUBLESHOOTING.md)

## What it does

- Keeps the final LE Audio render stream continuously alive, including real zero PCM during silence.
- Captures normal Windows render streams with Process Loopback while excluding the Relay worker itself.
- Automatically rebuilds the route after endpoint reconnect and Windows sleep/resume.
- Adds some latency compared with direct LE Audio, while our current E2E estimate remains below typical classic AAC/SBC A2DP ranges.

## Latency — estimated real playback E2E: ~55–75 ms

The table below is our current engineering estimate of **actual steady-state playback E2E**. These are not directly measured absolute values; they are inferred from relative acoustic tests plus the known software path and buffering budget.

| Path | Estimated actual E2E |
|---|---:|
| Realtek 3.5 mm wired | < ~20 ms |
| **LE Audio Relay (Process Loopback + GameEffects)** | **~55–75 ms** |
| Windows native LE Audio, hot / steady-state | ~80–120 ms |
| VB-CABLE + Windows Listen relay | ~120–150 ms |
| Native Bluetooth AAC | ~150–200 ms |
| Native Bluetooth SBC | ~180–250 ms |

> **These are speculative engineering estimates, not direct E2E measurements.**  
> Our actual test is a **relative comparison baseline** using chirps, a USB microphone, and cross-correlation. The raw coordinate contains Windows software-trigger delay, USB-mic/ADC delay, acoustic propagation, timestamp/reference offsets, and correlation-path bias. A raw result like “240 ms” therefore does **not** mean the user is actually hearing 240 ms of event-to-ear latency.

One important nuance: native Windows LE can look very good once its stream is already hot, but we measured about **350 ms of extra cold-start penalty** after teardown. Relay keeps the destination render/CIS hot specifically to avoid paying that startup cost during ordinary playback.

<details>
<summary>Relative test baseline, method, and raw coordinates</summary>

Test path:

```text
generated chirp
→ source path under test
→ physical output / earbuds
→ USB microphone
→ ADC / capture path
→ cross-correlation
```

These values are therefore useful for **A/B differences and ordering under the same setup**, not as absolute user event-to-ear latency.

Selected results:

| Path | Comparative test coordinate |
|---|---:|
| Realtek 3.5 mm wired → mic | median **60.10 ms** |
| Native Buds3 Pro LE, hot → mic | median **210.56 ms** |
| **Current Relay / GameEffects, SRC→MIC** | median **240.25 ms** |
| Legacy VB-CABLE relay → Buds3 Pro, hot | median **316.71 ms** |
| Native Buds3 Pro LE cold-start | hot baseline + ~**350.58 ms** startup penalty, about **559 ms** coordinate |

Current Relay / GameEffects repeatability:

- SRC→MIC median **240.25 ms**, range **238.94–240.78 ms**
- LOOP→MIC median **215.62 ms**, range **214.69–215.87 ms**
- SRC→LOOP about **24.5 ms**

Direct Realtek 10-run baseline:

```text
59.24, 60.50, 60.00, 60.17, 60.02,
60.27, 60.08, 60.12, 60.11, 60.08 ms
```

The useful conclusions are not the absolute coordinates themselves:

- current Process Loopback + GameEffects is materially shorter than the old VB-CABLE relay path;
- native LE is not inherently slow once already hot;
- teardown/cold-start can dominate native LE responsiveness in ordinary use;
- persistent render lets Relay remove much of that startup penalty from the normal playback path.

</details>

## Download

**[Download LEAudioRelay.exe — v0.21.0-daily.2](https://github.com/STanJK/LE-Audio-Relay/releases/download/v0.21.0-daily.2/LEAudioRelay.exe)**

Windows x64 · self-contained · single EXE · ~50 MB · no separate .NET runtime required

> [!WARNING]
> **Daily Test Candidate 2 is a prerelease and is currently hard-coded for Samsung Galaxy Buds3 Pro.**
>
> The prebuilt EXE looks for exactly one active render endpoint whose name contains `Galaxy Buds3 Pro`. General endpoint selection is planned for the next candidate.

## Quick start

1. Make sure Windows 11 LE Audio works and **Use LE Audio when available** is enabled.
2. Connect **Galaxy Buds3 Pro**.
3. Set the Windows default output to a **different active render endpoint** such as NVIDIA HDMI or Realtek.
   - Do **not** use the Buds as the Windows default output.
   - Do **not** use CABLE Input for the current baseline.
4. Run `LEAudioRelay.exe`.

A healthy tray state looks like:

```text
Running | Backend gen: N | Mode: GameEffects
```

LE Audio Relay automatically handles endpoint disconnect/reconnect and rebuilds the route after Windows resume.

## Advanced users: other LE Audio devices

Daily 2 can already be retargeted from source.

Check present audio endpoints:

```powershell
Get-PnpDevice -Class AudioEndpoint -PresentOnly |
    Select-Object Status,FriendlyName,InstanceId
```

Edit:

```text
Settings/RelayConfiguration.cs
```

Change:

```csharp
public string DestinationMatch { get; set; } =
    "Galaxy Buds3 Pro";
```

to a unique substring matching your target endpoint, then build:

```powershell
dotnet build .\LEAudioRelay.csproj -c Release
```

The internal worker `--dest` argument is not a supported standalone user CLI in Daily 2.

## More

- [Getting started](docs/GETTING_STARTED.md)
- [Why this exists](docs/WHY_THIS_EXISTS.md)
- [How it works](docs/HOW_IT_WORKS.md)
- [Validation status](docs/VALIDATION.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Contributing / bug reports](CONTRIBUTING.md)

If reporting a problem, include the Windows build, Bluetooth controller/driver, headset model, reproduction steps, and the relevant lifecycle log from:

```text
%LOCALAPPDATA%\LEAudioRelay\logs\
```

> No open-source license has been selected yet.
