namespace LEKeepAliveRelay.Config;

internal sealed class RelayOptions
{
    // Kept for source compatibility with the retired VB-CABLE code that may
    // still exist in an overlaid working tree. It is not used by the daily path.
    public string SourceNameContains { get; init; } = "CABLE Output";

    public string DestinationNameContains { get; init; } = "Galaxy Buds3 Pro";

    public int SampleRate { get; init; } = 48_000;
    public int Channels { get; init; } = 2;
    public int CaptureBufferMs { get; init; } = 10;
    public int RingCapacityMs { get; init; } = 80;
    public int TargetCushionMs { get; init; } = 10;
    public int StartupHoldMs { get; init; } = 40;

    // Health filter: ordinary 10/20 ms ring phase changes are intentionally silent.
    public int NormalFillUpperMs { get; init; } = 30;
    public int SustainedFillAlertSeconds { get; init; } = 3;
    public int MajorGlitchFrames { get; init; } = 480;
}
