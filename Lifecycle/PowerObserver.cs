using System.Windows.Forms;

namespace LEAudioRouter.Lifecycle;

/// <summary>
/// Thin WM_POWERBROADCAST observer. WndProc only updates an in-memory fact and
/// raises Changed; all route teardown/rebuild policy remains in supervision.
/// </summary>
internal sealed class PowerObserver :
    NativeWindow,
    IDisposable
{
    private const int WmPowerBroadcast = 0x0218;

    private const int PbtApmSuspend = 0x0004;
    private const int PbtApmResumeCritical = 0x0006;
    private const int PbtApmResumeSuspend = 0x0007;
    private const int PbtApmResumeAutomatic = 0x0012;

    private int _suspended;
    private long _revision;
    private long _suspendCount;
    private int _disposed;

    public PowerObserver()
    {
        CreateHandle(
            new CreateParams
            {
                Caption =
                    "LE Audio Router Power Observer"
            });
    }

    public event EventHandler? Changed;

    public PowerSnapshot Snapshot =>
        new(
            IsSuspended:
                Volatile.Read(
                    ref _suspended) != 0,

            Revision:
                Interlocked.Read(
                    ref _revision),

            SuspendCount:
                Interlocked.Read(
                    ref _suspendCount));

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0)
        {
            return;
        }

        DestroyHandle();
    }

    protected override void WndProc(
        ref Message m)
    {
        if (m.Msg ==
            WmPowerBroadcast)
        {
            int powerEvent =
                unchecked(
                    (int)m.WParam.ToInt64());

            switch (powerEvent)
            {
                case PbtApmSuspend:
                    if (Interlocked.Exchange(
                            ref _suspended,
                            1) == 0)
                    {
                        Interlocked.Increment(
                            ref _suspendCount);

                        Changed?.Invoke(
                            this,
                            EventArgs.Empty);
                    }

                    m.Result =
                        new IntPtr(1);

                    return;

                case PbtApmResumeAutomatic:
                case PbtApmResumeSuspend:
                    if (Interlocked.Exchange(
                            ref _suspended,
                            0) != 0)
                    {
                        Interlocked.Increment(
                            ref _revision);

                        Changed?.Invoke(
                            this,
                            EventArgs.Empty);
                    }

                    m.Result =
                        new IntPtr(1);

                    return;

                case PbtApmResumeCritical:
                    Interlocked.Exchange(
                        ref _suspended,
                        0);

                    // Critical resume can be delivered when the application did
                    // not receive the matching suspend notification. Either way
                    // the pre-resume route generation must not be trusted.
                    Interlocked.Increment(
                        ref _revision);

                    Changed?.Invoke(
                        this,
                        EventArgs.Empty);

                    m.Result =
                        new IntPtr(1);

                    return;
            }
        }

        base.WndProc(
            ref m);
    }
}
