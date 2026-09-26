namespace LEAudioRouter.Host;

internal enum ProcessMode
{
    Tray,
    Cli,
    BackendWorker
}

internal static class ProcessModeParser
{
    public static ProcessMode Parse(string[] args)
    {
        if (args.Any(x => x.Equals("--backend-worker", StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessMode.BackendWorker;
        }

        if (args.Any(x =>
                x.Equals("--cli", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--status", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--restart", StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessMode.Cli;
        }

        return ProcessMode.Tray;
    }
}
