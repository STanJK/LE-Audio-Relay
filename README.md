# LE Audio Relay

**A small Windows tray relay that keeps a Bluetooth LE Audio render stream alive and automatically rebuilds it across reconnects and sleep/resume.**

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
![Status](https://img.shields.io/badge/status-V0.21%20daily%20test-orange)
![Audio](https://img.shields.io/badge/audio-Bluetooth%20LE%20Audio-0A66C2)

[中文](README.zh-CN.md) · [Getting started](docs/GETTING_STARTED.md) · [Why this exists](docs/WHY_THIS_EXISTS.md) · [How it works](docs/HOW_IT_WORKS.md) · [Validation](docs/VALIDATION.md) · [Troubleshooting](docs/TROUBLESHOOTING.md)

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

## What it does

Normal apps render to the current Windows default endpoint. Separately, **Process Loopback** captures render streams while excluding the Relay worker process tree, then forwards the PCM to a persistent LE Audio render stream.

During silence, the destination stream stays alive with real zero PCM. This is the core workaround behind the project.

Current primary validation target: **Galaxy Buds3 Pro on Windows 11 LE Audio**.

## Status

V0.21 Daily Test Candidate 2 is under multi-day validation. The current focus is sleep/resume, reconnects, long-running playback, and the provisional positive-drift guard.

**VibeFactory** is already used in the real development workflow. It is currently private and planned for a separate public release; it is not required to build or run LE Audio Relay.

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
