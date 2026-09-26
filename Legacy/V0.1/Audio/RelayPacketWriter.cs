using LEKeepAliveRelay.Runtime;
using NAudio.CoreAudioApi;

namespace LEKeepAliveRelay.Audio;

internal static class RelayPacketWriter
{
    public static void Write(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        SpscPcmRing ring,
        RingRenderProvider provider,
        int startupHoldFrames,
        RelayRuntimeCounters counters)
    {
        int bytes = buffer.Length;
        if (bytes <= 0)
        {
            return;
        }

        int frames = bytes / ring.BlockAlign;
        if (frames <= 0)
        {
            return;
        }

        bool silent =
            (flags & AudioClientBufferFlags.Silent) != 0;

        int written;

        if (provider.RelayEnabled)
        {
            written =
                silent
                    ? ring.WriteZerosRuntime(frames)
                    : ring.WriteRuntime(buffer);
        }
        else
        {
            written =
                silent
                    ? ring.WriteZerosStartup(frames, startupHoldFrames)
                    : ring.WriteStartup(buffer, startupHoldFrames);
        }

        Interlocked.Increment(ref counters.CapturePackets);
        Interlocked.Add(ref counters.CaptureFrames, frames);
        Interlocked.Add(ref counters.CapturedBytes, bytes);
        Interlocked.Add(ref counters.WrittenFrames, written);

        if (silent)
        {
            Interlocked.Increment(ref counters.SilentPackets);
        }
    }
}
