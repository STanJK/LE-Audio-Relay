namespace LEAudioRouter.Settings;

internal sealed class DesiredRouterState
{
    public bool Enabled { get; set; } = true;

    public bool AutoReconnect { get; set; } = true;

    public RouterMode Mode { get; set; } = RouterMode.GameEffects;

    public string DestinationMatch { get; set; } = "Galaxy Buds3 Pro";
}
