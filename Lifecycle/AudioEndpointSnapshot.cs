namespace LEAudioRouter.Lifecycle;

internal enum TargetEndpointAvailability
{
    Absent,
    Available,
    Ambiguous
}

internal readonly record struct AudioEndpointSnapshot(
    TargetEndpointAvailability TargetAvailability,
    string? TargetDeviceId,
    string? TargetFriendlyName,
    int MatchingActiveTargetCount,
    string? DefaultRenderName,
    bool DefaultRenderSafe,
    string? BlockingReason);
