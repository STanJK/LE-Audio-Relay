namespace LEAudioRouter.Lifecycle;

internal readonly record struct PowerSnapshot(
    bool IsSuspended,
    long Revision);
