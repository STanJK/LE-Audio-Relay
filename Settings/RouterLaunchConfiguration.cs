namespace LEAudioRouter.Settings;

internal readonly record struct RouterLaunchConfiguration(
    RouterMode Mode,
    string DestinationMatch);
