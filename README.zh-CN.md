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

## 延迟：我们估计实际 E2E 约 55–75 ms

下面这张表是我们目前对**实际 steady-state playback E2E** 的工程估计。它不是直接测出来的绝对值，而是结合相对声学测试、已知软件路径和各层 buffer/period 做的推测。

| 路径 | 推测实际 E2E |
|---|---:|
| Realtek 3.5 mm 有线 | < ~20 ms |
| **LE Audio Relay（Process Loopback + GameEffects）** | **~55–75 ms** |
| Windows 原生 LE Audio，hot / steady-state | ~80–120 ms |
| VB-CABLE + Windows Listen relay | ~120–150 ms |
| Native Bluetooth AAC | ~150–200 ms |
| Native Bluetooth SBC | ~180–250 ms |

> **这些数字是 speculative engineering estimates，不是直接 E2E 测量结果。**  
> 我们真正做过的测试是一套**相对对比基线**：chirp + USB microphone + cross-correlation。原始坐标混入了 Windows 软件触发路径、USB mic、ADC、声学传播、时间戳/相关检测等固定和可变延迟，所以不能看到“240 ms”就理解成实际听感真的有 240 ms。

一个重要细节是：Windows 原生 LE 在已经 hot 的 stream 上，比较坐标可以很低；但我们实测过 **silence teardown → cold start** 会额外引入约 **350 ms** 的启动罚时。Relay 持续保持目的端 render/CIS hot，正是为了绕开这类日常起播延迟。

<details>
<summary>相对测试基线、方法与原始坐标</summary>

测试方法：

```text
generated chirp
→ source path under test
→ physical output / earbuds
→ USB microphone
→ ADC / capture path
→ cross-correlation
```

因此这些数字只适合做**同一装置、同一方法下的 A/B 差值和排序**，不适合直接当作用户事件到耳朵的绝对 E2E。

部分结果：

| 路径 | 对比测试坐标 |
|---|---:|
| Realtek 3.5 mm 有线 → mic | median **60.10 ms** |
| Native Buds3 Pro LE，hot → mic | median **210.56 ms** |
| **Current Relay / GameEffects，SRC→MIC** | median **240.25 ms** |
| Legacy VB-CABLE relay → Buds3 Pro，hot | median **316.71 ms** |
| Native Buds3 Pro LE cold-start | hot 基线 + ~**350.58 ms** startup penalty，约 **559 ms** 坐标 |

Current Relay / GameEffects 的重复结果：

- SRC→MIC median **240.25 ms**, range **238.94–240.78 ms**
- LOOP→MIC median **215.62 ms**, range **214.69–215.87 ms**
- SRC→LOOP 约 **24.5 ms**

Direct Realtek 10-run：

```text
59.24, 60.50, 60.00, 60.17, 60.02,
60.27, 60.08, 60.12, 60.11, 60.08 ms
```

这里最重要的不是绝对值，而是：

- current Process Loopback + GameEffects 比旧 VB-CABLE relay 路径明显更短；
- native LE hot 本身并不慢；
- native LE 日常最大的坑之一是 teardown 后的 cold-start/onset penalty；
- Relay 用 persistent render 把这类 cold-start 从正常播放路径里尽量拿掉。

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
