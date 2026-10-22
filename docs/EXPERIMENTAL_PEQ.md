# Experimental Buds3 Pro PEQ

Standalone experiment on branch `experimental/buds3pro-peq`. Defaults to **OFF**. The main branch is unchanged.

The render provider filters the Float32/48k/stereo output **after** PcmRelayBoundary. It does not modify the capture, clock correction, PCM ring, lifecycle or WASAPI flags.

## Enable

Stop the tray app completely (including its worker). In a PowerShell terminal:

```powershell
$env:LERELAY_PEQ = "showcase"
dotnet run -c Release --project .\LEAudioRelay.csproj
```

Supported values: `off`, `reference`, `showcase`, `kino`, `diagnostic`.

`diagnostic` is **-30 dB gain only**, for immediate audible path verification. Other presets use a -3 dB preamp. The setting is read when the route is created, not on every render callback; restart the app to change preset. If launching an installed EXE instead of dotnet run, set the environment variable before starting that EXE.

## Caveats

No UI, live updates, dynamic EQ, clipping limiter or crossfeed in this experiment. The original bypass is bit-identical because the processor is not instantiated when off. With EQ enabled, state runs across render callbacks; zero-filled keepalive samples are processed as normal zero samples. Any eventual source/device rate change needs revalidation. Shelves use the RBJ slope-S convention, not a generic device-specific Q.

Not a calibrated Samsung frequency-response correction. Listen at moderate levels and compare.
