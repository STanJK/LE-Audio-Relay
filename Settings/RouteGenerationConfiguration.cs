namespace LEAudioRouter.Settings;

internal readonly record struct RouteGenerationConfiguration(
    RouterMode Mode,
    string DestinationMatch);
