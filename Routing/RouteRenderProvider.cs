using LEAudioRelay.Timing;
using NAudio.Wave;

namespace LEAudioRelay.Routing;

internal sealed class RouteRenderProvider :
    IWaveProvider
{
    private readonly PcmRelayBoundary _boundary;

    public RouteRenderProvider(
        WaveFormat format,
        PcmRelayBoundary boundary)
    {
        WaveFormat = format;
        _boundary = boundary;
    }

    public WaveFormat WaveFormat
    {
        get;
    }

    public int Read(
        Span<byte> buffer) =>
        _boundary.FillRenderBuffer(
            buffer);
}
