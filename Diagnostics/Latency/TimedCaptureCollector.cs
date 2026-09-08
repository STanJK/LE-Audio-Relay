using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Diagnostics.Latency;

internal sealed class TimedCaptureCollector
{
    private readonly object _gate = new();
    private readonly WaveFormat _format;
    private readonly List<float> _samples = new(500_000);
    private readonly List<PacketStamp> _packets = new(4096);

    public TimedCaptureCollector(WaveFormat format)
    {
        _format = format;
        AudioSampleReader.EnsureSupported(format);
    }

    public void AddPacket(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        long qpc100ns)
    {
        int frames = buffer.Length / _format.BlockAlign;

        if (frames <= 0)
        {
            return;
        }

        lock (_gate)
        {
            int startFrame = _samples.Count;

            _packets.Add(
                new PacketStamp(
                    startFrame,
                    frames,
                    qpc100ns));

            if ((flags & AudioClientBufferFlags.Silent) != 0)
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    _samples.Add(0f);
                }

                return;
            }

            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * _format.BlockAlign;

                _samples.Add(
                    AudioSampleReader.ReadChannelZero(
                        buffer,
                        offset,
                        _format));
            }
        }
    }

    public CaptureSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new CaptureSnapshot(
                _samples.ToArray(),
                _packets.ToArray(),
                _format.SampleRate);
        }
    }
}
