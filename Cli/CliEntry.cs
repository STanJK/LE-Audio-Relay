using LEAudioRelay.Host;

namespace LEAudioRelay.Cli;

internal static class CliEntry
{
    public static int Run(string[] args)
    {
        ConsoleBridge.AttachToParent();

        if (args.Any(x => x.Equals("--status", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(
                "Round4 shell bootstrap is present; backend status IPC is not implemented yet.");

            return 0;
        }

        if (args.Any(x => x.Equals("--restart", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine(
                "CLI restart requires the future shell control interface and is not implemented yet.");

            return 2;
        }

        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("LE Audio Relay - Round4 shell bootstrap");
        Console.WriteLine();
        Console.WriteLine("No arguments     Start the Windows tray shell");
        Console.WriteLine("--status         Print current bootstrap status");
        Console.WriteLine("--restart        Reserved for shell IPC restart");
        Console.WriteLine("--backend-worker Reserved backend generation entry");
    }
}
