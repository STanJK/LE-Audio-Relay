using LEAudioRouter.Cli;
using LEAudioRouter.Host;
using LEAudioRouter.Shell;

namespace LEAudioRouter;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ProcessMode mode = ProcessModeParser.Parse(args);

        return mode switch
        {
            ProcessMode.Cli => CliEntry.Run(args),
            ProcessMode.BackendWorker => BackendWorkerPlaceholder.Run(args),
            _ => RunTray()
        };
    }

    private static int RunTray()
    {
        ApplicationConfiguration.Initialize();

        using var context = new TrayApplicationContext();
        Application.Run(context);

        return 0;
    }
}
