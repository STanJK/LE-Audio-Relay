using LEAudioRelay.DSP;
using LEAudioRelay.Timing;
using NAudio.Wave;

namespace LEAudioRelay.Routing;

internal sealed class RouteRenderProvider : IWaveProvider
{
    private readonly PcmRelayBoundary _boundary;
    private readonly RelayEqualizer? _equalizer;

    public RouteRenderProvider(WaveFormat format, PcmRelayBoundary boundary)
    {
        WaveFormat = format;
        _boundary = boundary;
        _equalizer = RelayEqualizer.FromEnvironment();
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<byte> buffer)
    {
        int bytes = _boundary.FillRenderBuffer(buffer);
        if (_equalizer is not null && bytes > 0)
            _equalizer.Process(buffer.Slice(0, bytes));
        return bytes;
    }
}
