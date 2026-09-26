using System.Diagnostics;
using System.Runtime.Versioning;
using LEKeepAliveRelay.Audio;
using LEKeepAliveRelay.Config;
using LEKeepAliveRelay.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Runtime;

[SupportedOSPlatform("windows")]
internal static class ProcessLoopbackRelay
{
    public static int Run(string[] args)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            Console.Error.WriteLine(
                "ERROR: Process Loopback requires Windows 10 build 20348 or newer.");
            return 2;
        }

        var options = new RelayOptions();

        string destinationMatch =
            CliArguments.Get(
                args,
                "--dest",
                options.DestinationNameContains);

        AudioCategorySelection category =
            AudioCategoryOption.Parse(
                args);

        try
        {
            using var finder = new DeviceFinder();
            using var destination = finder.FindRender(destinationMatch);

            AudioFormatGuard.Ensure48kFloatStereo(destination);
            ValidateDefaultRouting(finder, destinationMatch);

            WaveFormat format =
                WaveFormat.CreateIeeeFloatWaveFormat(
                    options.SampleRate,
                    options.Channels);

            int ringCapacityFrames =
                options.SampleRate * options.RingCapacityMs / 1000;

            int targetCushionFrames =
                options.SampleRate * options.TargetCushionMs / 1000;

            int startupHoldFrames =
                options.SampleRate * options.StartupHoldMs / 1000;

            var ring =
                new SpscPcmRing(
                    ringCapacityFrames,
                    format.BlockAlign);

            var provider =
                new RingRenderProvider(
                    format,
                    ring,
                    targetCushionFrames);

            var playerBuilder =
                new WasapiPlayerBuilder()
                    .WithDevice(destination)
                    .WithSharedMode()
                    .WithEventSync()
                    .WithLowLatency()
                    .WithMmcssThreadPriority("Pro Audio");

            if (category.Value is
                AudioStreamCategory streamCategory)
            {
                playerBuilder.WithCategory(
                    streamCategory);
            }

            using var player =
                playerBuilder.Build();

            player.Init(provider);

            using var capture =
                ProcessLoopbackCapture.CreateExcludeProcessTree(
                    (uint)Environment.ProcessId,
                    format,
                    options.CaptureBufferMs);

            var counters = new RelayRuntimeCounters();

            capture.Packet +=
                (buffer, flags, devicePosition, qpcPosition) =>
                {
                    try
                    {
                        RelayPacketWriter.Write(
                            buffer,
                            flags,
                            ring,
                            provider,
                            startupHoldFrames,
                            counters);
                    }
                    catch
                    {
                        Interlocked.Increment(ref counters.CallbackErrors);
                    }
                };

            using var quit = new ManualResetEventSlim(false);

            ConsoleCancelEventHandler cancelHandler =
                (_, e) =>
                {
                    e.Cancel = true;
                    quit.Set();
                };

            Console.CancelKeyPress += cancelHandler;

            try
            {
                Console.WriteLine("LEKeepAliveRelay");
                Console.WriteLine($"Process Loopback -> {destination.FriendlyName}");
                Console.WriteLine(
                    $"48 kHz Float32 Stereo | category {category.DisplayName} | " +
                    $"target ring {options.TargetCushionMs} ms | Ctrl+C to stop");

                // Destination first: keep the final Buds AudioClient/CIS alive from the start.
                player.Play();
                Thread.Sleep(250);

                provider.ArmRelay();
                capture.Start();

                Console.WriteLine("RUNNING");

                var runtime = Stopwatch.StartNew();
                var health = new RuntimeHealthFilter(options);

                // Let the one-time activation transient settle. Nothing inside this
                // window is treated as a daily runtime problem.
                if (quit.Wait(2000))
                {
                    provider.DisableRelay();
                    capture.Stop();
                    Thread.Sleep(100);
                    player.Stop();
                    return 0;
                }

                long runtimeUnderBaseline = ring.UnderrunFrames;
                long runtimeOverBaseline = ring.OverflowFrames;
                long callbackBaseline = Interlocked.Read(ref counters.CallbackErrors);

                health.Prime(ring, provider, counters);

                while (!quit.Wait(1000))
                {
                    foreach (HealthEvent healthEvent in health.Observe(ring, provider, counters))
                    {
                        string prefix =
                            healthEvent.Severity == HealthSeverity.Error
                                ? "ERROR"
                                : "WARN";

                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss}] {prefix}: {healthEvent.Message}");
                    }
                }

                runtime.Stop();

                // Return to real-zero keepalive before capture stops so shutdown is not counted
                // as a runtime underrun.
                provider.DisableRelay();
                capture.Stop();
                Thread.Sleep(100);
                player.Stop();

                long runtimeUnder = ring.UnderrunFrames - runtimeUnderBaseline;
                long runtimeOver = ring.OverflowFrames - runtimeOverBaseline;
                long runtimeCallbackErrors =
                    Interlocked.Read(ref counters.CallbackErrors) - callbackBaseline;

                Console.WriteLine(
                    $"STOPPED | runtime={FormatRuntime(runtime.Elapsed)} | " +
                    $"under={runtimeUnder} | over={runtimeOver} | " +
                    $"cbErr={runtimeCallbackErrors}");

                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");

            if (CliArguments.Has(args, "--debug"))
            {
                Console.Error.WriteLine(ex);
            }

            return 1;
        }
    }

    private static void ValidateDefaultRouting(
        DeviceFinder finder,
        string destinationMatch)
    {
        string defaultRender = finder.GetDefaultRenderName();

        bool pointsToDestination =
            defaultRender.Contains(
                destinationMatch,
                StringComparison.OrdinalIgnoreCase) ||
            defaultRender.Contains(
                "Galaxy Buds3 Pro",
                StringComparison.OrdinalIgnoreCase);

        bool pointsToCable =
            defaultRender.Contains(
                "CABLE Input",
                StringComparison.OrdinalIgnoreCase);

        if (pointsToDestination || pointsToCable)
        {
            throw new InvalidOperationException(
                "Windows default output must be a sacrificial physical sink " +
                "(for example the active NVIDIA HDMI endpoint), not Buds or CABLE Input. " +
                $"Current default: {defaultRender}");
        }
    }

    private static string FormatRuntime(TimeSpan elapsed)
    {
        if (elapsed.TotalDays >= 1)
        {
            return $"{(int)elapsed.TotalDays}d {elapsed:hh\\:mm\\:ss}";
        }

        return elapsed.ToString(@"hh\:mm\:ss");
    }
}
