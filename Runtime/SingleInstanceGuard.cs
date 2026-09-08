namespace LEKeepAliveRelay.Runtime;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\LEKeepAliveRelay",
            createdNew: out bool createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstanceGuard(mutex);
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
        }

        _mutex.Dispose();
    }
}
