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

        // No arguments is the product's normal tray entry.
        // Any explicit argument is treated as CLI intent so an unknown or
        // help flag can never accidentally leave a background tray process.
        return args.Length == 0
            ? ProcessMode.Tray
            : ProcessMode.Cli;
    }
}
