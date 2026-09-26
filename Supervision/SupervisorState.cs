namespace LEAudioRouter.Supervision;

internal enum SupervisorState
{
    Idle,
    WaitingForBackend,
    Running,
    RestartRequested,
    Stopped
}
