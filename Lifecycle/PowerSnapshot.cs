namespace LEAudioRelay.Lifecycle;

internal readonly record struct PowerSnapshot(
    bool IsSuspended,
    long Revision,
    long SuspendCount);
