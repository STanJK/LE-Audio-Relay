using NAudio.CoreAudioApi;

namespace LEAudioRouter.Lifecycle;

/// <summary>
/// Re-enumeration is the authoritative source of endpoint reality.
/// Notifications only wake the supervisor so it can call Probe again.
/// </summary>
internal sealed class AudioEndpointProbe :
    IDisposable
{
    private readonly MMDeviceEnumerator _enumerator =
        new();

    public AudioEndpointSnapshot Probe(
        string destinationMatch)
    {
        List<MMDevice> activeRenderDevices =
            _enumerator
                .EnumerateAudioEndPoints(
                    DataFlow.Render,
                    DeviceState.Active)
                .ToList();

        try
        {
            List<MMDevice> targetMatches =
                activeRenderDevices
                    .Where(
                        device =>
                            device.FriendlyName.Contains(
                                destinationMatch,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            TargetEndpointAvailability targetAvailability =
                targetMatches.Count switch
                {
                    0 =>
                        TargetEndpointAvailability.Absent,

                    1 =>
                        TargetEndpointAvailability.Available,

                    _ =>
                        TargetEndpointAvailability.Ambiguous
                };

            MMDevice? target =
                targetMatches.Count == 1
                    ? targetMatches[0]
                    : null;

            string? defaultRenderName =
                TryGetDefaultRenderName();

            bool defaultRenderSafe =
                IsDefaultRenderSafe(
                    defaultRenderName,
                    destinationMatch);

            string? blockingReason =
                BuildBlockingReason(
                    targetAvailability,
                    targetMatches.Count,
                    defaultRenderName,
                    defaultRenderSafe,
                    destinationMatch);

            return new AudioEndpointSnapshot(
                TargetAvailability:
                    targetAvailability,

                TargetDeviceId:
                    target?.ID,

                TargetFriendlyName:
                    target?.FriendlyName,

                MatchingActiveTargetCount:
                    targetMatches.Count,

                DefaultRenderName:
                    defaultRenderName,

                DefaultRenderSafe:
                    defaultRenderSafe,

                BlockingReason:
                    blockingReason);
        }
        finally
        {
            foreach (MMDevice device
                     in activeRenderDevices)
            {
                device.Dispose();
            }
        }
    }

    public void Dispose() =>
        _enumerator.Dispose();

    private string? TryGetDefaultRenderName()
    {
        try
        {
            using MMDevice device =
                _enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia);

            return device.FriendlyName;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDefaultRenderSafe(
        string? defaultRenderName,
        string destinationMatch)
    {
        if (string.IsNullOrWhiteSpace(
                defaultRenderName))
        {
            return false;
        }

        bool pointsToDestination =
            defaultRenderName.Contains(
                destinationMatch,
                StringComparison.OrdinalIgnoreCase) ||
            defaultRenderName.Contains(
                "Galaxy Buds3 Pro",
                StringComparison.OrdinalIgnoreCase);

        bool pointsToCable =
            defaultRenderName.Contains(
                "CABLE Input",
                StringComparison.OrdinalIgnoreCase);

        return
            !pointsToDestination &&
            !pointsToCable;
    }

    private static string? BuildBlockingReason(
        TargetEndpointAvailability availability,
        int targetCount,
        string? defaultRenderName,
        bool defaultRenderSafe,
        string destinationMatch)
    {
        if (availability ==
            TargetEndpointAvailability.Ambiguous)
        {
            return
                $"Found {targetCount} active render endpoints matching " +
                $"\"{destinationMatch}\"; expected exactly one.";
        }

        if (availability ==
            TargetEndpointAvailability.Available &&
            !defaultRenderSafe)
        {
            return string.IsNullOrWhiteSpace(
                    defaultRenderName)
                ? "No default multimedia render endpoint is available."
                : "Windows default output must be a sacrificial physical sink, " +
                  $"not Buds or CABLE Input. Current default: {defaultRenderName}";
        }

        return null;
    }
}
