namespace LEKeepAliveRelay.Diagnostics;

// The old monolithic latency lab was retired during the Process Loopback refactor.
// Keep this tiny compatibility entry point so stale commands fail clearly instead
// of silently running the daily relay.
internal static class QuickLatencyProbe
{
    public static int Run(string[] args)
    {
        Console.WriteLine(
            "The legacy --latency-test modes were retired in the modular refactor.");
        Console.WriteLine();
        Console.WriteLine(
            "Use the current hot-path measurement instead:");
        Console.WriteLine();
        Console.WriteLine(
            "  --process-loopback-poc --hot-latency --dest \"Galaxy Buds3 Pro\" " +
            "--sink \"Q27G40ZDF\" --mic \"USB_MIC\" --runs 10");

        return 2;
    }
}
