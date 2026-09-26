# LE Audio Relay

**一个保持 Windows 蓝牙 LE Audio render stream 持续存活，并在断联、重连和睡眠恢复后自动重建音频路径的小型托盘工具。**

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
![Status](https://img.shields.io/badge/status-V0.21%20daily%20test-orange)
![Audio](https://img.shields.io/badge/audio-Bluetooth%20LE%20Audio-0A66C2)

[English](README.md) · [快速开始](docs/GETTING_STARTED.md) · [为什么做这个](docs/WHY_THIS_EXISTS.md) · [工作原理](docs/HOW_IT_WORKS.md) · [验证状态](docs/VALIDATION.md) · [故障排查](docs/TROUBLESHOOTING.md)

## 下载

**[下载 LEAudioRelay.exe — v0.21.0-daily.2](https://github.com/STanJK/LE-Audio-Relay/releases/download/v0.21.0-daily.2/LEAudioRelay.exe)**

Windows x64 · Self-contained · 单 EXE · 约 50 MB · 无需另外安装 .NET Runtime

> [!WARNING]
> **Daily Test Candidate 2 是预发布版本，而且当前预编译 EXE 硬编码针对 Samsung Galaxy Buds3 Pro。**
>
> 它会寻找唯一一个名称包含 `Galaxy Buds3 Pro` 的 Active render endpoint。通用 endpoint 选择会在下一版加入。

## 快速使用

1. 确认 Windows 11 LE Audio 正常，并开启 **可用时使用 LE Audio**。
2. 连接 **Galaxy Buds3 Pro**。
3. 把 Windows 默认输出设为**另一个 Active render endpoint**，例如 NVIDIA HDMI 或 Realtek。
   - 不要把 Buds 设为 Windows 默认输出。
   - 当前基线不要使用 CABLE Input。
4. 运行 `LEAudioRelay.exe`。

正常托盘状态：

```text
Running | Backend gen: N | Mode: GameEffects
```

断联/重连和 Windows sleep/resume 会由 Relay 自动重建 route。

## 高级用户：测试其他 LE Audio 设备

Daily 2 已经可以通过改源码切换目标。

查看当前音频 endpoint：

```powershell
Get-PnpDevice -Class AudioEndpoint -PresentOnly |
    Select-Object Status,FriendlyName,InstanceId
```

修改：

```text
Settings/RelayConfiguration.cs
```

把：

```csharp
public string DestinationMatch { get; set; } =
    "Galaxy Buds3 Pro";
```

改成能唯一匹配目标 endpoint 的字符串，然后：

```powershell
dotnet build .\LEAudioRelay.csproj -c Release
```

Daily 2 内部 worker 的 `--dest` 不是可直接使用的用户 CLI。

## 它做了什么

普通应用照常渲染到 Windows 默认输出。与此同时，**Process Loopback** 捕获系统 render stream，并排除 Relay worker 自己，再把 PCM 持续送到 LE Audio 目标。

静音时最终 render stream 仍持续输出真实 zero PCM，不让目的端 AudioClient 因普通静音而消失。这是项目最核心的 workaround。

当前主要验证目标：**Windows 11 LE Audio + Galaxy Buds3 Pro**。

## 当前状态

V0.21 Daily Test Candidate 2 正在做多日日测，重点是 sleep/resume、断联重连、长时间运行和临时 positive-drift guard。

**VibeFactory** 已经实际用于本项目开发流程；目前仍为 Private，计划之后单独公开。运行或编译 LE Audio Relay 不依赖 VibeFactory。

## 更多文档

- [快速开始](docs/GETTING_STARTED.md)
- [为什么做这个](docs/WHY_THIS_EXISTS.md)
- [工作原理](docs/HOW_IT_WORKS.md)
- [验证状态](docs/VALIDATION.md)
- [故障排查](docs/TROUBLESHOOTING.md)
- [架构](docs/ARCHITECTURE.md)
- [贡献 / Bug 报告](CONTRIBUTING.md)

提问题时最好附上 Windows build、蓝牙控制器/驱动、耳机型号、复现步骤，以及：

```text
%LOCALAPPDATA%\LEAudioRelay\logs\
```

中的相关 lifecycle log。

> 当前尚未选定正式开源许可证。
