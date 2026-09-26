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

## Latency — closer to LE Audio than old-school A2DP

These numbers are **comparative acoustic measurements**, not calibrated absolute event-to-ear E2E latency.

| Path | Measured median |
|---|---:|
| Direct Realtek 3.5 mm → mic | **60.10 ms** |
| Relay → Realtek 3.5 mm → mic | **147.61 ms** |
| Direct Buds3 Pro LE, hot stream → mic | **210.56 ms** |
| Relay → Buds3 Pro LE, hot stream → mic | **316.71 ms** |

In the same measurement coordinate, Relay added about **87.5 ms** over direct Realtek and **106.2 ms** over direct Buds3 Pro LE.

| Path | Rough E2E estimate |
|---|---:|
| Native / direct LE Audio | ~80–120 ms |
| **LE Audio Relay** | **~120–150 ms** |
| Classic Bluetooth AAC | ~150–200 ms |
| Classic Bluetooth SBC | ~180–250 ms |

> These E2E ranges are **speculative engineering estimates, not measurements and not codec specifications**. Actual latency depends on Windows, controller/driver, device buffering, codec/QoS, and workload.

<details>
<summary>Comparative test method and raw results</summary>

We used a generated chirp/correlation signal, acoustic capture with a USB microphone, and cross-correlation to locate the received signal.

The absolute coordinate therefore includes fixed USB mic / ADC and acoustic-path delay. The useful result is the **difference between paths measured with the same setup**.

- Relay → Buds3 Pro early 5-run series: 331.29, 322.82, 321.66, 316.66, 320.11 ms; median 321.66 ms.
- Relay → Buds3 Pro hot steady-state: 315.90–317.25 ms; median 316.71 ms.
- Direct Realtek 10-run series: 59.24, 60.50, 60.00, 60.17, 60.02, 60.27, 60.08, 60.12, 60.11, 60.08 ms; median 60.10 ms.
- Relay → Realtek hot: 138.34–157.59 ms; median 147.61 ms.
- Direct Buds3 Pro hot: 209.63–210.91 ms; median 210.56 ms.
- Direct Buds cold-start showed about 350.58 ms of startup overhead; excluded from the steady-state table.

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
