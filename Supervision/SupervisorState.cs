namespace LEAudioRouter.Supervision;

internal enum SupervisorState
{
    Idle,
    Starting,
    Running,
    RestartRequested,
    Restarting,
    Faulted,
    Stopped
}
