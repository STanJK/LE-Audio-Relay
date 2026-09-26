namespace LEAudioRelay.Settings;

internal sealed class RelayConfiguration
{
    public RelayMode Mode { get; set; } =
        RelayMode.GameEffects;

    public string DestinationMatch { get; set; } =
        "Galaxy Buds3 Pro";

    public RouteGenerationConfiguration Snapshot() =>
        new(
            Mode,
            DestinationMatch);
}
