using System.Buffers.Binary;
using System.Diagnostics;
using NAudio.Wave;

namespace LEKeepAliveRelay.Diagnostics.Latency;

internal static class SignalGenerator
{
    public const int WarmupMs = 3000;
    public const int IntervalMs = 1000;
    public const int TailMs = 800;
    public const int ChirpDurationMs = 60;

    private const double ChirpStartHz = 1200.0;
    private const double ChirpEndHz = 9000.0;
    private const float ChirpAmplitude = 0.25f;

    public static byte[] GenerateHotSignal(
        WaveFormat format,
        int runs,
        out long[] chirpStartBytes)
    {
        int totalMs =
            WarmupMs +
            (runs - 1) * IntervalMs +
            ChirpDurationMs +
            TailMs;

        int totalFrames = format.SampleRate * totalMs / 1000;
        int chirpFrames = format.SampleRate * ChirpDurationMs / 1000;

        byte[] data = new byte[totalFrames * format.BlockAlign];
        chirpStartBytes = new long[runs];

        Span<byte> span = data;

        for (int run = 0; run < runs; run++)
        {
            int chirpStartFrame =
                format.SampleRate *
                (WarmupMs + run * IntervalMs) /
                1000;

            chirpStartBytes[run] =
                (long)chirpStartFrame * format.BlockAlign;

            for (int n = 0; n < chirpFrames; n++)
            {
                float value = ChirpSample(n, chirpFrames, format.SampleRate);
                int frame = chirpStartFrame + n;
                int frameOffset = frame * format.BlockAlign;

                for (int channel = 0; channel < format.Channels; channel++)
                {
                    int offset = frameOffset + channel * 4;

                    BinaryPrimitives.WriteInt32LittleEndian(
                        span.Slice(offset, 4),
                        BitConverter.SingleToInt32Bits(value));
                }
            }
        }

        return data;
    }

    public static float[] GenerateReferenceChirp(int sampleRate)
    {
        int frames = sampleRate * ChirpDurationMs / 1000;
        var result = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            result[i] = ChirpSample(i, frames, sampleRate);
        }

        return result;
    }

    public static long WaitUntilRenderedPosition(
        WasapiPlayer player,
        long targetBytePosition,
        WaveFormat format)
    {
        var timeout = Stopwatch.StartNew();

        while (true)
        {
            long positionBytes = player.GetPosition();

            if (positionBytes >= targetBytePosition)
            {
                long now = Qpc100nsNow();
                long overshootBytes = positionBytes - targetBytePosition;
                long overshootFrames = overshootBytes / format.BlockAlign;
                long overshoot100ns =
                    overshootFrames * 10_000_000L / format.SampleRate;

                return now - overshoot100ns;
            }

            if (timeout.ElapsedMilliseconds > 30_000)
            {
                throw new TimeoutException(
                    "Injector AudioClock did not reach chirp position.");
            }

            long remainingBytes = targetBytePosition - positionBytes;
            long remainingFrames = remainingBytes / format.BlockAlign;

            if (remainingFrames > format.SampleRate / 50)
            {
                Thread.Sleep(2);
            }
            else
            {
                Thread.SpinWait(128);
            }
        }
    }

    public static long Qpc100nsNow() =>
        Stopwatch.GetTimestamp() * 10_000_000L / Stopwatch.Frequency;

    private static float ChirpSample(
        int index,
        int totalFrames,
        int sampleRate)
    {
        double t = index / (double)sampleRate;
        double duration = totalFrames / (double)sampleRate;
        double slope = (ChirpEndHz - ChirpStartHz) / duration;

        double phase =
            2.0 * Math.PI *
            (ChirpStartHz * t + 0.5 * slope * t * t);

        double window =
            totalFrames <= 1
                ? 1.0
                : 0.5 -
                  0.5 * Math.Cos(
                      2.0 * Math.PI * index / (totalFrames - 1));

        return
            ChirpAmplitude *
            (float)(Math.Sin(phase) * window);
    }
}
