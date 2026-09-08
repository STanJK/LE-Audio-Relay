using NAudio.CoreAudioApi;

namespace LEKeepAliveRelay.Core;

internal readonly record struct AudioCategorySelection(
    AudioStreamCategory? Value,
    string CliName,
    string DisplayName)
{
    public bool IsExplicit =>
        Value.HasValue;
}

internal static class AudioCategoryOption
{
    public static AudioCategorySelection Parse(
        string[] args)
    {
        string raw =
            CliArguments.Get(
                    args,
                    "--category",
                    "default")
                .Trim()
                .ToLowerInvariant();

        return raw switch
        {
            "default" or
            "unset" or
            "none" =>
                new AudioCategorySelection(
                    null,
                    "default",
                    "Default / unset"),

            "other" =>
                new AudioCategorySelection(
                    AudioStreamCategory.Other,
                    "other",
                    nameof(AudioStreamCategory.Other)),

            "media" =>
                new AudioCategorySelection(
                    AudioStreamCategory.Media,
                    "media",
                    nameof(AudioStreamCategory.Media)),

            "game-media" or
            "gamemedia" =>
                new AudioCategorySelection(
                    AudioStreamCategory.GameMedia,
                    "game-media",
                    nameof(AudioStreamCategory.GameMedia)),

            "game-effects" or
            "gameeffects" or
            "game" =>
                new AudioCategorySelection(
                    AudioStreamCategory.GameEffects,
                    "game-effects",
                    nameof(AudioStreamCategory.GameEffects)),

            _ =>
                throw new ArgumentException(
                    "Invalid --category value. " +
                    "Use: default, other, media, game-media, or game-effects.")
        };
    }
}
