namespace LEKeepAliveRelay.Diagnostics.Latency;

internal sealed class CaptureSnapshot
{
    public float[] Samples { get; }
    public PacketStamp[] Packets { get; }
    public int SampleRate { get; }

    public CaptureSnapshot(
        float[] samples,
        PacketStamp[] packets,
        int sampleRate)
    {
        Samples = samples;
        Packets = packets;
        SampleRate = sampleRate;
    }

    public int FindFrameAtOrAfterQpc(long targetQpc100ns)
    {
        if (Packets.Length == 0)
        {
            return 0;
        }

        foreach (PacketStamp packet in Packets)
        {
            long packetEndQpc =
                packet.Qpc100ns +
                packet.FrameCount * 10_000_000L / SampleRate;

            if (targetQpc100ns > packetEndQpc)
            {
                continue;
            }

            if (targetQpc100ns <= packet.Qpc100ns)
            {
                return packet.StartFrame;
            }

            long delta100ns = targetQpc100ns - packet.Qpc100ns;

            int offsetFrames =
                (int)(
                    delta100ns * SampleRate / 10_000_000L);

            return Math.Clamp(
                packet.StartFrame + offsetFrames,
                0,
                Samples.Length);
        }

        return Samples.Length;
    }

    public long QpcForFrame(int globalFrame)
    {
        foreach (PacketStamp packet in Packets)
        {
            int packetEnd = packet.StartFrame + packet.FrameCount;

            if (globalFrame < packet.StartFrame || globalFrame >= packetEnd)
            {
                continue;
            }

            int offsetFrames = globalFrame - packet.StartFrame;

            return
                packet.Qpc100ns +
                offsetFrames * 10_000_000L / SampleRate;
        }

        throw new InvalidOperationException(
            "Unable to map detected frame to QPC.");
    }
}
