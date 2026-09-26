using NAudio.CoreAudioApi;

namespace LEAudioRouter.Routing;

internal sealed class AudioEndpointResolver :
    IDisposable
{
    private readonly MMDeviceEnumerator _enumerator =
        new();

    public MMDevice FindUniqueActiveRender(
        string nameContains)
    {
        var devices =
            _enumerator
                .EnumerateAudioEndPoints(
                    DataFlow.Render,
                    DeviceState.Active)
                .ToList();

        var matches =
            devices
                .Where(
                    device =>
                        device.FriendlyName.Contains(
                            nameContains,
                            StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (matches.Count != 1)
        {
            string available =
                string.Join(
                    Environment.NewLine,
                    devices.Select(
                        device =>
                            $"  - {device.FriendlyName}"));

            foreach (MMDevice device in devices)
            {
                device.Dispose();
            }

            throw new InvalidOperationException(
                $"Expected exactly one active render endpoint matching " +
                $"\"{nameContains}\", found {matches.Count}." +
                Environment.NewLine +
                "Active render endpoints:" +
                Environment.NewLine +
                available);
        }

        MMDevice selected =
            matches[0];

        foreach (MMDevice device in devices)
        {
            if (!ReferenceEquals(
                    device,
                    selected))
            {
                device.Dispose();
            }
        }

        return selected;
    }

    public string GetDefaultRenderName()
    {
        using MMDevice device =
            _enumerator.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);

        return device.FriendlyName;
    }

    public void Dispose() =>
        _enumerator.Dispose();
}
