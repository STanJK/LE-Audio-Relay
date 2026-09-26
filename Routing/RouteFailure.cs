namespace LEAudioRouter.Routing;

internal readonly record struct RouteFailure(
    string Reason,
    Exception? Exception)
{
    public override string ToString() =>
        Exception is null
            ? Reason
            : $"{Reason}: {Exception.Message}";
}
