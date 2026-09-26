using System.Runtime.Versioning;
using LEKeepAliveRelay.Core;
using LEKeepAliveRelay.Diagnostics;
using LEKeepAliveRelay.Diagnostics.Latency;
using LEKeepAliveRelay.Runtime;

namespace LEKeepAliveRelay;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (CliArguments.Has(args, "--help") ||
            CliArguments.Has(args, "-h") ||
            CliArguments.Has(args, "/?"))
        {
            PrintHelp();
            return 0;
        }

        // Hidden child used by the hot-latency probe. Never acquire the
        // single-instance mutex here.
        if (CliArguments.Has(args, "--injector-worker"))
        {
            return InjectorWorker.Run(args);
        }

        if (CliArguments.Has(args, "--hot-latency"))
        {
            return ProcessLoopbackLatencyProbe.Run(args);
        }

        if (CliArguments.Has(args, "--latency-test"))
        {
            return QuickLatencyProbe.Run(args);
        }

        using SingleInstanceGuard? instance = SingleInstanceGuard.TryAcquire();

        if (instance is null)
        {
            // Normal daily behavior: do not spam an error if Task Scheduler or
            // a double-click tries to start a second copy.
            return 0;
        }

        // No arguments = daily Process Loopback architecture.
        // --process-loopback-poc remains accepted as a compatibility alias.
        return ProcessLoopbackRelay.Run(args);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("LEKeepAliveRelay - Process Loopback edition");
        Console.WriteLine();
        Console.WriteLine("Daily:");
        Console.WriteLine("  LEKeepAliveRelay.exe");
        Console.WriteLine("  LEKeepAliveRelay.exe --dest \"Galaxy Buds3 Pro\"");
        Console.WriteLine(
            "  LEKeepAliveRelay.exe --category game-effects");
        Console.WriteLine();
        Console.WriteLine("Hot latency diagnostic:");
        Console.WriteLine(
            "  LEKeepAliveRelay.exe --hot-latency " +
            "--category media --dest \"Galaxy Buds3 Pro\" " +
            "--sink \"Q27G40ZDF\" --mic \"USB_MIC\" --runs 10");
        Console.WriteLine();
        Console.WriteLine("Audio stream category:");
        Console.WriteLine(
            "  --category default | other | media | game-media | game-effects");
        Console.WriteLine(
            "  Omit --category to preserve the previous unset/default behavior.");
        Console.WriteLine();
        Console.WriteLine("Debug:");
        Console.WriteLine("  --debug    Print full exception details on fatal errors.");
        Console.WriteLine();
        Console.WriteLine("Routing requirement:");
        Console.WriteLine(
            "  Windows default output must be a sacrificial physical sink " +
            "(for example the active NVIDIA HDMI endpoint), not Buds or CABLE Input.");
    }
}
