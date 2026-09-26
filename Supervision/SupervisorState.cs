namespace LEAudioRouter.Supervision;

internal enum SupervisorState
{
    Idle,
    WaitingForEndpoint,
    TopologyBlocked,
    Starting,
    Running,
    RestartRequested,
    Restarting,
    RecoveringFault,
    Stopped
}
