using System.Diagnostics;
using LEKeepAliveRelay.Audio;
using LEKeepAliveRelay.Config;
using LEKeepAliveRelay.Core;
using LEKeepAliveRelay.Diagnostics.Latency;
using LEKeepAliveRelay.Runtime;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Diagnostics;

internal static class ProcessLoopbackLatencyProbe
{
    private const string DefaultSinkMatch = "Q27G40ZDF";
    private const string DefaultMicMatch = "USB_MIC";

    public static int Run(string[] args)
    {
        var options = new RelayOptions();

        int runs =
            CliArguments.GetInt(args, "--runs", 10, 1, 30);

        string destinationMatch =
            CliArguments.Get(
                args,
                "--dest",
                options.DestinationNameContains);

        string sinkMatch =
            CliArguments.Get(
                args,
                "--sink",
                DefaultSinkMatch);

        string micMatch =
            CliArguments.Get(
                args,
                "--mic",
                DefaultMicMatch);

        AudioCategorySelection category =
            AudioCategoryOption.Parse(
                args);

        WaveFormat format =
            WaveFormat.CreateIeeeFloatWaveFormat(
                options.SampleRate,
                options.Channels);

        using var finder = new DeviceFinder();
        using var destination = finder.FindRender(destinationMatch);
        using var microphone = finder.FindCapture(micMatch);

        AudioFormatGuard.Ensure48kFloatStereo(destination);

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

        using var micRecorder =
            new WasapiRecorderBuilder()
                .WithDevice(microphone)
                .WithSharedMode()
                .WithEventSync()
                .WithLowLatency()
                .WithMmcssThreadPriority("Pro Audio")
                .Build();

        var micCollector =
            new TimedCaptureCollector(
                micRecorder.WaveFormat);

        micRecorder.DataAvailable +=
            (buffer, flags, devicePosition, qpcPosition) =>
                micCollector.AddPacket(
                    buffer,
                    flags,
                    qpcPosition);

        using var micStopped = new ManualResetEventSlim(false);
        micRecorder.RecordingStopped += (_, _) => micStopped.Set();

        string token = Guid.NewGuid().ToString("N");
        string readyEventName = $@"Local\LEKeepAliveRelay_HotReady_{token}";
        string startEventName = $@"Local\LEKeepAliveRelay_HotStart_{token}";
        string resultPath =
            Path.Combine(
                Path.GetTempPath(),
                $"LEKeepAliveRelay_Hot_{token}.txt");

        using var childReady =
            new EventWaitHandle(
                false,
                EventResetMode.ManualReset,
                readyEventName);

        using var childStart =
            new EventWaitHandle(
                false,
                EventResetMode.ManualReset,
                startEventName);

        Process? injector = null;
        ProcessLoopbackCapture? loopCapture = null;

        var loopCollector = new TimedCaptureCollector(format);
        var counters = new RelayRuntimeCounters();

        try
        {
            injector =
                InjectorWorker.StartChild(
                    sinkMatch,
                    runs,
                    readyEventName,
                    startEventName,
                    resultPath);

            if (!childReady.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "Injector child did not become ready.");
            }

            if (injector.HasExited)
            {
                throw new InvalidOperationException(
                    "Injector child exited before the test started. " +
                    InjectorWorker.ReadDiagnostics(injector));
            }

            loopCapture =
                ProcessLoopbackCapture.CreateIncludeProcessTree(
                    (uint)injector.Id,
                    format,
                    options.CaptureBufferMs);

            loopCapture.Packet +=
                (buffer, flags, devicePosition, qpcPosition) =>
                {
                    try
                    {
                        loopCollector.AddPacket(
                            buffer,
                            flags,
                            qpcPosition);

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

            PrintHeader(
                destination,
                sinkMatch,
                microphone,
                injector.Id,
                runs,
                category);

            // One destination stream/CIS for the whole test.
            player.Play();
            Thread.Sleep(2500);

            provider.ArmRelay();
            micRecorder.StartRecording();
            Thread.Sleep(150);

            loopCapture.Start();
            Thread.Sleep(150);

            childStart.Set();

            int expectedMs =
                SignalGenerator.WarmupMs +
                (runs - 1) * SignalGenerator.IntervalMs +
                SignalGenerator.ChirpDurationMs +
                SignalGenerator.TailMs;

            var childTimeout = Stopwatch.StartNew();

            while (!injector.WaitForExit(100))
            {
                if (childTimeout.ElapsedMilliseconds > expectedMs + 10_000)
                {
                    try
                    {
                        injector.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    throw new TimeoutException(
                        "Injector child timed out.");
                }
            }

            if (injector.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Injector child failed with exit code {injector.ExitCode}. " +
                    InjectorWorker.ReadDiagnostics(injector));
            }

            Thread.Sleep(700);

            provider.DisableRelay();
            loopCapture.Stop();
            Thread.Sleep(100);

            micRecorder.StopRecording();
            micStopped.Wait(TimeSpan.FromSeconds(1));

            player.Stop();

            InjectorTiming timing =
                InjectorWorker.ReadTiming(
                    resultPath,
                    runs);

            CaptureSnapshot loopSnapshot = loopCollector.Snapshot();
            CaptureSnapshot micSnapshot = micCollector.Snapshot();

            float[] loopReference =
                SignalGenerator.GenerateReferenceChirp(
                    loopSnapshot.SampleRate);

            float[] micReference =
                SignalGenerator.GenerateReferenceChirp(
                    micSnapshot.SampleRate);

            var sourceToLoop = new List<double>(runs);
            var loopToMic = new List<double>(runs);
            var sourceToMic = new List<double>(runs);

            Console.WriteLine(
                $"Injector cold-start overhead: {timing.ColdStartOverheadMs:F2} ms");
            Console.WriteLine();
            Console.WriteLine("HOT measurements:");
            Console.WriteLine();

            for (int i = 0; i < runs; i++)
            {
                long sourceQpc = timing.ChirpQpc100ns[i];

                CorrelationHit loopHit =
                    CorrelationAnalyzer.FindTimed(
                        loopSnapshot,
                        loopReference,
                        sourceQpc - 100L * 10_000L,
                        sourceQpc + 250L * 10_000L);

                CorrelationHit micHit =
                    CorrelationAnalyzer.FindTimed(
                        micSnapshot,
                        micReference,
                        sourceQpc + 5L * 10_000L,
                        sourceQpc + 550L * 10_000L);

                if (loopHit.Score < 0.20 || micHit.Score < 0.10)
                {
                    Console.WriteLine(
                        $"#{i + 1:D2}  REJECTED " +
                        $"loopCorr={loopHit.Score:F3} micCorr={micHit.Score:F3}");
                    continue;
                }

                double sourceLoopMs =
                    (loopHit.Qpc100ns - sourceQpc) / 10_000.0;

                double loopMicMs =
                    (micHit.Qpc100ns - loopHit.Qpc100ns) / 10_000.0;

                double sourceMicMs =
                    (micHit.Qpc100ns - sourceQpc) / 10_000.0;

                sourceToLoop.Add(sourceLoopMs);
                loopToMic.Add(loopMicMs);
                sourceToMic.Add(sourceMicMs);

                Console.WriteLine(
                    $"#{i + 1:D2}  " +
                    $"SRC->LOOP {sourceLoopMs,7:F2} ms   " +
                    $"LOOP->MIC {loopMicMs,7:F2} ms   " +
                    $"SRC->MIC {sourceMicMs,7:F2} ms   " +
                    $"corr={loopHit.Score:F3}/{micHit.Score:F3}");
            }

            Console.WriteLine();

            if (sourceToLoop.Count == 0)
            {
                Console.WriteLine("No valid hot latency measurements.");
                return 4;
            }

            PrintSummary("SRC -> LOOP", sourceToLoop);
            PrintSummary("LOOP -> MIC", loopToMic);
            PrintSummary("SRC -> MIC", sourceToMic);

            Console.WriteLine();
            Console.WriteLine($"Final ring fill   : {ring.FillFrames} frames");
            Console.WriteLine($"Runtime underrun  : {ring.UnderrunFrames}");
            Console.WriteLine($"Runtime overflow  : {ring.OverflowFrames}");
            Console.WriteLine($"Underrun zero-fill: {provider.UnderrunZeroFillFrames}");
            Console.WriteLine($"Startup input drop: {ring.StartupDroppedIncomingFrames}");
            Console.WriteLine($"Startup trim      : {provider.StartupDiscardFrames}");
            Console.WriteLine(
                $"Callback errors   : {Interlocked.Read(ref counters.CallbackErrors)}");

            return 0;
        }
        finally
        {
            try { provider.DisableRelay(); } catch { }
            try { loopCapture?.Stop(); } catch { }
            try { micRecorder.StopRecording(); } catch { }
            try { player.Stop(); } catch { }

            loopCapture?.Dispose();

            if (injector is not null)
            {
                if (!injector.HasExited)
                {
                    try { injector.Kill(entireProcessTree: true); } catch { }
                }

                injector.Dispose();
            }

            try { File.Delete(resultPath); } catch { }
        }
    }

    private static void PrintHeader(
        MMDevice destination,
        string sinkMatch,
        MMDevice microphone,
        int injectorPid,
        int runs,
        AudioCategorySelection category)
    {
        Console.WriteLine("LEKeepAliveRelay - Process Loopback HOT Latency");
        Console.WriteLine("================================================");
        Console.WriteLine();
        Console.WriteLine($"Destination  : {destination.FriendlyName}");
        Console.WriteLine($"Injector sink: {sinkMatch}");
        Console.WriteLine($"Microphone   : {microphone.FriendlyName}");
        Console.WriteLine($"Capture mode : INCLUDE injector PID {injectorPid} only");
        Console.WriteLine($"Category     : {category.DisplayName}");
        Console.WriteLine("Format       : 48000 Hz / Float32 / Stereo");
        Console.WriteLine($"Warmup       : {SignalGenerator.WarmupMs} ms");
        Console.WriteLine($"Chirps       : {runs}, one every {SignalGenerator.IntervalMs} ms");
        Console.WriteLine();
        Console.WriteLine("Keep the injector sink physically silent/unplugged.");
        Console.WriteLine("Place one Buds earpiece close to USB_MIC.");
        Console.WriteLine();
    }

    private static void PrintSummary(
        string label,
        List<double> values)
    {
        double[] sorted = values.OrderBy(x => x).ToArray();

        double median =
            (sorted.Length & 1) == 1
                ? sorted[sorted.Length / 2]
                : (sorted[sorted.Length / 2 - 1] +
                   sorted[sorted.Length / 2]) / 2.0;

        Console.WriteLine(label);
        Console.WriteLine($"  median : {median:F2} ms");
        Console.WriteLine(
            $"  min/max: {sorted.First():F2} / {sorted.Last():F2} ms");
    }
}
