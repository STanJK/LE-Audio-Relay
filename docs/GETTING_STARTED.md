# Getting started

This guide gets the current LE Audio Router daily-test candidate running from source.

> [!IMPORTANT]
> There is currently no signed installer or prebuilt public release. The candidate is source-only while full daily-use validation is in progress.

## Requirements

### Windows

Use Windows 11 on an x64 PC.

Microsoft's current LE Audio support requirements include:

- Windows 11 version 22H2 or newer;
- compatible Bluetooth LE hardware and audio codec support;
- LE Audio capable drivers from the PC manufacturer for both the Bluetooth radio and audio subsystem.

The simplest user-facing check is:

**Settings → Bluetooth & devices → Devices → Use LE Audio when available**

If that setting is not present, Windows does not currently consider the PC LE Audio capable.

Microsoft reference:

- https://support.microsoft.com/en-us/windows/hardware/bluetooth/check-if-a-windows-11-device-supports-bluetooth-low-energy-audio

### Earbuds or headset

The current primary target is:

- **Samsung Galaxy Buds3 Pro**

Other LE Audio devices may work, but the project does not yet claim general compatibility.

### .NET

Install the .NET 10 SDK.

Check:

```powershell
dotnet --version
```

### A sacrificial physical render endpoint

LE Audio Router intentionally does **not** use the Buds as the normal Windows default output.

You need another active physical render endpoint, for example:

- NVIDIA HDMI/DisplayPort audio;
- Realtek analog output;
- another stable physical audio endpoint.

Do not use:

- Galaxy Buds3 Pro as the Windows default output;
- VB-CABLE / CABLE Input for the current daily baseline.

The sacrificial endpoint does not need to be audible. It only needs to remain a valid Windows render target for ordinary applications.

## Verify Windows LE Audio first

Before testing the router, confirm native LE Audio works at all.

1. Pair the LE Audio device with Windows.
2. Confirm **Use LE Audio when available** is enabled.
3. Confirm Windows exposes the expected render endpoint.
4. Play audio directly to the device at least once.
5. If Windows cannot use the device natively, fix that before involving LE Audio Router.

LE Audio Router is not a Bluetooth driver and cannot create LE Audio support on a PC whose Windows/driver stack does not provide it.

## Clone and build

```powershell
git clone https://github.com/STanJK/le-audio-windows-relay.git
cd le-audio-windows-relay
```

For the current development documentation branch:

```powershell
git fetch origin
git switch docs/public-readme
```

For normal local testing, build Release:

```powershell
dotnet clean .\LEAudioRouter.csproj
dotnet build .\LEAudioRouter.csproj -c Release
```

The executable is:

```text
bin\Release\net10.0-windows\LEAudioRouter.exe
```

## Configure Windows before launch

1. Connect the Buds.
2. Open **Settings → System → Sound**.
3. Set the Windows default output to the sacrificial physical endpoint.
4. Keep Spatial Sound disabled for the current validation baseline.
5. Do not set the Buds as default output.

Example:

```text
Windows default render
    = NVIDIA HDMI

LE Audio Router destination
    = Galaxy Buds3 Pro
```

## Start LE Audio Router

```powershell
.\bin\Release\net10.0-windows\LEAudioRouter.exe
```

The program should appear in the system tray.

A healthy startup should eventually show:

```text
Running | Backend gen: N | Mode: GameEffects
```

## Verify the process model

While Running:

```powershell
Get-CimInstance Win32_Process -Filter "Name='LEAudioRouter.exe'" |
    Select-Object ProcessId,ParentProcessId,CommandLine
```

Expected:

```text
1 tray/supervisor process
1 --backend-worker process
```

When the Buds are disconnected, only the tray process should remain.

## Tray controls

### Mode

Available render categories:

- GameEffects
- GameMedia
- Media
- Default / unset

GameEffects is the current daily default because it performed best in this project's comparative testing. That is a project choice, not a universal Microsoft recommendation.

### Restart audio route

This destroys the current worker generation and builds a fresh one.

Use it when:

- comparing categories;
- recovering from an unusual user-mode route state;
- collecting reproducible lifecycle evidence.

### Exit

Exit is the canonical way to stop routing.

There is no separate enabled/disabled state.

## Check lifecycle logs

Logs are stored at:

```text
%LOCALAPPDATA%\LEAudioRouter\logs\
```

Open the folder:

```powershell
explorer "$env:LOCALAPPDATA\LEAudioRouter\logs"
```

Tail the newest log:

```powershell
$log = Get-ChildItem "$env:LOCALAPPDATA\LEAudioRouter\logs\lifecycle-*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Get-Content $log.FullName -Wait
```

The persistent journal is intentionally low-volume. It does not contain per-second heartbeat or ring warnings.

## Basic lifecycle test

### Disconnect

Disconnect the Buds.

Expected:

```text
Running
→ WaitingForEndpoint
→ Backend gen: none
```

There should be no repeated worker PID churn.

### Reconnect

Reconnect the Buds.

Expected:

```text
WaitingForEndpoint
→ Starting
→ Running
```

One fresh worker generation should appear.

### Sleep/resume

Put Windows to sleep, then resume.

Expected:

```text
old worker generation
→ stale after PowerRevision change
→ endpoint re-probe
→ fresh worker generation
→ Running
```

Sleep/resume is still part of the V0.21 full daily validation round.

## Stop

Right-click the tray icon and choose **Exit**.

## Next

- [Why this exists](WHY_THIS_EXISTS.md)
- [How it works](HOW_IT_WORKS.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Validation](VALIDATION.md)
