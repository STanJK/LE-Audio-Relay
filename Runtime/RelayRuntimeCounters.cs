namespace LEKeepAliveRelay.Runtime;

internal sealed class RelayRuntimeCounters
{
    public long CapturePackets;
    public long CaptureFrames;
    public long CapturedBytes;
    public long WrittenFrames;
    public long SilentPackets;
    public long CallbackErrors;
}
