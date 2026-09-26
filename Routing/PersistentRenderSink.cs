using LEAudioRouter.Settings;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEAudioRouter.Routing;

internal sealed class PersistentRenderSink :
    IDisposable
{
    private readonly WasapiPlayer _player;

    private PersistentRenderSink(
        WasapiPlayer player)
    {
        _player = player;

        _player.PlaybackStopped +=
            OnPlaybackStopped;
    }

    public event Action<Exception?>? Stopped;

    public static PersistentRenderSink Create(
        MMDevice destination,
        RouterMode mode,
        IWaveProvider provider)
    {
        var builder =
            new WasapiPlayerBuilder()
                .WithDevice(destination)
                .WithSharedMode()
                .WithEventSync()
                .WithLowLatency()
                .WithMmcssThreadPriority(
                    "Pro Audio");

        AudioStreamCategory? category =
            RenderCategoryPolicy.Resolve(
                mode);

        if (category is
            AudioStreamCategory explicitCategory)
        {
            builder.WithCategory(
                explicitCategory);
        }

        WasapiPlayer player =
            builder.Build();

        player.Init(provider);

        return new PersistentRenderSink(
            player);
    }

    public void Start() =>
        _player.Play();

    public void Stop() =>
        _player.Stop();

    public void Dispose()
    {
        _player.PlaybackStopped -=
            OnPlaybackStopped;

        _player.Dispose();
    }

    private void OnPlaybackStopped(
        object? sender,
        StoppedEventArgs e) =>
        Stopped?.Invoke(
            e.Exception);
}
