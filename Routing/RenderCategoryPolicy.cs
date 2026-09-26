using LEAudioRelay.Settings;
using NAudio.CoreAudioApi;

namespace LEAudioRelay.Routing;

internal static class RenderCategoryPolicy
{
    public static AudioStreamCategory? Resolve(
        RelayMode mode) =>
        mode switch
        {
            RelayMode.GameEffects =>
                AudioStreamCategory.GameEffects,

            RelayMode.GameMedia =>
                AudioStreamCategory.GameMedia,

            RelayMode.Media =>
                AudioStreamCategory.Media,

            RelayMode.Default =>
                null,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(mode))
        };
}
