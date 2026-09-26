namespace LEAudioRouter.Settings;

internal sealed class RouterConfiguration
{
    public RouterMode Mode { get; set; } =
        RouterMode.GameEffects;

    public string DestinationMatch { get; set; } =
        "Galaxy Buds3 Pro";

    public RouteGenerationConfiguration Snapshot() =>
        new(
            Mode,
            DestinationMatch);
}
