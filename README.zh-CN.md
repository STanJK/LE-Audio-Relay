# LE Audio Relay

**一个用于持续保持 Windows 蓝牙 LE Audio 播放、额外延迟相对较低，并自动处理断联重连与 sleep/resume 的系统托盘音频中继。**

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
![Status](https://img.shields.io/badge/status-V0.21%20daily%20test-orange)
![Audio](https://img.shields.io/badge/audio-Bluetooth%20LE%20Audio-0A66C2)

[English](README.md) · [快速开始](docs/GETTING_STARTED.md) · [为什么做这个](docs/WHY_THIS_EXISTS.md) · [工作原理](docs/HOW_IT_WORKS.md) · [验证状态](docs/VALIDATION.md) · [故障排查](docs/TROUBLESHOOTING.md)

## 它做什么

- 最终 LE Audio render stream 始终保持存活，静音时也持续输出真实 zero PCM。
- 用 Process Loopback 捕获普通 Windows render stream，并排除 Relay worker 自己。
- 耳机断联重连或 Windows sleep/resume 后自动重建 route。
- 相比 direct LE Audio 会增加一部分延迟，但我们当前的 E2E 估计仍低于传统 AAC/SBC A2DP 的典型范围。

## 延迟：更接近 LE Audio，而不是传统 A2DP

下面的数字是**纯对比用的声学测试**，不是经过绝对标定的 event-to-ear E2E 延迟。

| 路径 | 实测 median |
|---|---:|
| Direct Realtek 3.5 mm → mic | **60.10 ms** |
| Relay → Realtek 3.5 mm → mic | **147.61 ms** |
| Direct Buds3 Pro LE，hot stream → mic | **210.56 ms** |
| Relay → Buds3 Pro LE，hot stream → mic | **316.71 ms** |

在同一套测量坐标里，Relay 相对 direct Realtek 约增加 **87.5 ms**，相对 direct Buds3 Pro LE 约增加 **106.2 ms**。

| 路径 | 粗略 E2E 估计 |
|---|---:|
| Native / direct LE Audio | ~80–120 ms |
| **LE Audio Relay** | **~120–150 ms** |
| 传统 Bluetooth AAC | ~150–200 ms |
| 传统 Bluetooth SBC | ~180–250 ms |

> 上面这组 E2E 区间是**工程推测，不是实测结果，也不是 codec 规格值**。实际延迟取决于 Windows、控制器/驱动、耳机 buffer、codec/QoS 和 workload。

<details>
<summary>对比测试方法与原始数据</summary>

使用生成的 chirp / correlation 信号，通过 USB 麦克风做声学采集，再用 cross-correlation 定位收到的信号。

因此绝对值包含 USB mic / ADC 和声学路径等固定延迟；真正有意义的是**同一测试装置下不同路径之间的差值**。

- Relay → Buds3 Pro 早期 5-run：331.29、322.82、321.66、316.66、320.11 ms；median 321.66 ms。
- Relay → Buds3 Pro hot steady-state：315.90–317.25 ms；median 316.71 ms。
- Direct Realtek 10-run：59.24、60.50、60.00、60.17、60.02、60.27、60.08、60.12、60.11、60.08 ms；median 60.10 ms。
- Relay → Realtek hot：138.34–157.59 ms；median 147.61 ms。
- Direct Buds3 Pro hot：209.63–210.91 ms；median 210.56 ms。
- Direct Buds cold-start 测得约 350.58 ms 的启动额外开销；不计入 steady-state 表。

</details>
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
