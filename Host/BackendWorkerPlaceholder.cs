namespace LEAudioRouter.Host;

internal static class BackendWorkerPlaceholder
{
    public static int Run(string[] args)
    {
        Console.Error.WriteLine(
            "Backend worker mode is reserved by the Round4 architecture and is not implemented yet.");

        return 2;
    }
}
