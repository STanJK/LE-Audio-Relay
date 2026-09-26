using LEAudioRouter.Settings;

namespace LEAudioRouter.Supervision;

internal sealed class BackendSupervisor : IDisposable
{
    private readonly object _gate = new();
    private readonly DesiredRouterState _desiredState;

    private SupervisorState _state = SupervisorState.WaitingForBackend;
    private long _restartGeneration;

    public BackendSupervisor(DesiredRouterState desiredState)
    {
        _desiredState = desiredState;
    }

    public event EventHandler? StateChanged;

    public SupervisorState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public long RestartGeneration
    {
        get
        {
            lock (_gate)
            {
                return _restartGeneration;
            }
        }
    }

    public void Start()
    {
        SetState(SupervisorState.WaitingForBackend);
    }

    public void RequestRestart()
    {
        lock (_gate)
        {
            _restartGeneration++;
            _state = SupervisorState.RestartRequested;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnabled(bool enabled)
    {
        _desiredState.Enabled = enabled;

        SetState(
            enabled
                ? SupervisorState.WaitingForBackend
                : SupervisorState.Stopped);
    }

    public void SetMode(RouterMode mode)
    {
        if (_desiredState.Mode == mode)
        {
            return;
        }

        _desiredState.Mode = mode;
        RequestRestart();
    }

    public void SetAutoReconnect(bool enabled)
    {
        _desiredState.AutoReconnect = enabled;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        SetState(SupervisorState.Stopped);
    }

    private void SetState(SupervisorState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
