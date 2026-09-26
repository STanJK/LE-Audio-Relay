using LEAudioRelay.Cli;
using LEAudioRelay.Host;
using LEAudioRelay.Shell;

namespace LEAudioRelay;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ProcessMode mode = ProcessModeParser.Parse(args);

        return mode switch
        {
            ProcessMode.Cli => CliEntry.Run(args),
            ProcessMode.BackendWorker => BackendWorker.Run(args),
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
