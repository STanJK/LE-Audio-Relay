using NAudio.CoreAudioApi;

namespace LEKeepAliveRelay.Audio;

internal sealed class DeviceFinder : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public MMDevice FindRender(string nameContains) =>
        FindUniqueActive(DataFlow.Render, nameContains);

    public MMDevice FindCapture(string nameContains) =>
        FindUniqueActive(DataFlow.Capture, nameContains);

    public MMDevice FindUniqueActive(DataFlow flow, string nameContains)
    {
        var devices =
            _enumerator
                .EnumerateAudioEndPoints(flow, DeviceState.Active)
                .ToList();

        var matches =
            devices
                .Where(d =>
                    d.FriendlyName.Contains(
                        nameContains,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (matches.Count != 1)
        {
            string available = string.Join(
                Environment.NewLine,
                devices.Select(d => $"  - {d.FriendlyName}"));

            foreach (MMDevice device in devices)
            {
                device.Dispose();
            }

            throw new InvalidOperationException(
                $"Expected exactly one active {flow} endpoint matching " +
                $"\"{nameContains}\", found {matches.Count}." +
                Environment.NewLine +
                "Active endpoints:" + Environment.NewLine +
                available);
        }

        MMDevice selected = matches[0];

        foreach (MMDevice device in devices)
        {
            if (!ReferenceEquals(device, selected))
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

    public void Dispose() => _enumerator.Dispose();
}
