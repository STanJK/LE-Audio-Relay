namespace LEKeepAliveRelay.Diagnostics.Latency;

internal sealed record InjectorTiming(
    double ColdStartOverheadMs,
    long[] ChirpQpc100ns);

internal readonly record struct CorrelationResult(
    int StartFrame,
    double Score);

internal readonly record struct CorrelationHit(
    long Qpc100ns,
    double Score);

internal readonly record struct PacketStamp(
    int StartFrame,
    int FrameCount,
    long Qpc100ns);
