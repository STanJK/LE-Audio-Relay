using LEAudioRouter.Settings;
using NAudio.CoreAudioApi;

namespace LEAudioRouter.Routing;

internal static class RenderCategoryPolicy
{
    public static AudioStreamCategory? Resolve(
        RouterMode mode) =>
        mode switch
        {
            RouterMode.GameEffects =>
                AudioStreamCategory.GameEffects,

            RouterMode.GameMedia =>
                AudioStreamCategory.GameMedia,

            RouterMode.Media =>
                AudioStreamCategory.Media,

            RouterMode.Default =>
                null,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(mode))
        };
}
