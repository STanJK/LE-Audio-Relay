namespace LEAudioRelay.Settings;

internal readonly record struct RouteGenerationConfiguration(
    RelayMode Mode,
    string DestinationMatch);
