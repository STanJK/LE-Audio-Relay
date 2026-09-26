using LEKeepAliveRelay.Core;
using LEKeepAliveRelay.Diagnostics.Latency;
using LEKeepAliveRelay.Runtime;

namespace LEKeepAliveRelay.Diagnostics;

// Compatibility facade for commands used during development.
internal static class ProcessLoopbackPoc
{
    public static int Run(string[] args)
    {
        if (CliArguments.Has(args, "--injector-worker"))
        {
            return InjectorWorker.Run(args);
        }

        if (CliArguments.Has(args, "--hot-latency"))
        {
            return ProcessLoopbackLatencyProbe.Run(args);
        }

        return ProcessLoopbackRelay.Run(args);
    }
}
