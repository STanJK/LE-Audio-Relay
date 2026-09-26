using NAudio.CoreAudioApi;

namespace LEAudioRouter.Lifecycle;

/// <summary>
/// Converts Windows Core Audio endpoint notifications into one cheap wake-up
/// signal. Notification callbacks run on the Windows audio worker thread and
/// MUST NOT enumerate endpoints, dispose audio objects, or run recovery policy.
/// </summary>
internal sealed class AudioEndpointObserver :
    IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDeviceNotificationClient _notifications;

    private int _disposed;

    public AudioEndpointObserver()
    {
        _enumerator =
            new MMDeviceEnumerator();

        _notifications =
            _enumerator.CreateNotificationClient(
                useSynchronizationContext: false);

        _notifications.DeviceAdded +=
            OnTopologyChanged;

        _notifications.DeviceRemoved +=
            OnTopologyChanged;

        _notifications.DeviceStateChanged +=
            OnTopologyChanged;

        _notifications.DefaultDeviceChanged +=
            OnTopologyChanged;
    }

    public event EventHandler? TopologyChanged;

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0)
        {
            return;
        }

        _notifications.DeviceAdded -=
            OnTopologyChanged;

        _notifications.DeviceRemoved -=
            OnTopologyChanged;

        _notifications.DeviceStateChanged -=
            OnTopologyChanged;

        _notifications.DefaultDeviceChanged -=
            OnTopologyChanged;

        _notifications.Dispose();
        _enumerator.Dispose();
    }

    private void OnTopologyChanged(
        object? sender,
        EventArgs e)
    {
        // This callback may be running on a Core Audio worker thread.
        // The subscriber must only coalesce/wake supervision.
        TopologyChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
