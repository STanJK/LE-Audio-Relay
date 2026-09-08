using System.Diagnostics;
using System.Globalization;
using LEKeepAliveRelay.Audio;
using LEKeepAliveRelay.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Diagnostics.Latency;

internal static class InjectorWorker
{
    public static int Run(string[] args)
    {
        string sinkMatch = CliArguments.GetRequired(args, "--sink");
        string readyEventName = CliArguments.GetRequired(args, "--ready-event");
        string startEventName = CliArguments.GetRequired(args, "--start-event");
        string resultPath = CliArguments.GetRequired(args, "--result-file");

        int runs =
            CliArguments.GetInt(
                args,
                "--runs",
                10,
                1,
                30);

        using var finder = new DeviceFinder();
        using var sink = finder.FindRender(sinkMatch);

        AudioFormatGuard.Ensure48kFloatStereo(sink);

        WaveFormat format =
            WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

        byte[] signal =
            SignalGenerator.GenerateHotSignal(
                format,
                runs,
                out long[] chirpStartBytes);

        using var rawStream =
            new RawSourceWaveStream(
                new MemoryStream(signal, writable: false),
                format);

        using var player =
            new WasapiPlayerBuilder()
                .WithDevice(sink)
                .WithSharedMode()
                .WithEventSync()
                .WithLowLatency()
                .WithMmcssThreadPriority("Pro Audio")
                .Build();

        player.Init(rawStream);

        using var readyEvent = EventWaitHandle.OpenExisting(readyEventName);
        using var startEvent = EventWaitHandle.OpenExisting(startEventName);

        readyEvent.Set();

        if (!startEvent.WaitOne(TimeSpan.FromSeconds(15)))
        {
            return 10;
        }

        long playQpc = SignalGenerator.Qpc100nsNow();
        player.Play();

        var chirpQpc = new long[runs];

        for (int i = 0; i < runs; i++)
        {
            chirpQpc[i] =
                SignalGenerator.WaitUntilRenderedPosition(
                    player,
                    chirpStartBytes[i],
                    format);
        }

        double coldStartOverheadMs =
            (chirpQpc[0] - playQpc) / 10_000.0 -
            SignalGenerator.WarmupMs;

        int expectedMs =
            SignalGenerator.WarmupMs +
            (runs - 1) * SignalGenerator.IntervalMs +
            SignalGenerator.ChirpDurationMs +
            SignalGenerator.TailMs;

        var timeout = Stopwatch.StartNew();

        while (player.PlaybackState == PlaybackState.Playing)
        {
            if (timeout.ElapsedMilliseconds > expectedMs + 5000)
            {
                player.Stop();
                return 11;
            }

            Thread.Sleep(5);
        }

        using var writer =
            new StreamWriter(
                resultPath,
                append: false);

        writer.WriteLine(
            coldStartOverheadMs.ToString(
                "R",
                CultureInfo.InvariantCulture));

        foreach (long qpc in chirpQpc)
        {
            writer.WriteLine(
                qpc.ToString(CultureInfo.InvariantCulture));
        }

        return 0;
    }

    public static Process StartChild(
        string sinkMatch,
        int runs,
        string readyEventName,
        string startEventName,
        string resultPath)
    {
        string? processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException(
                "Environment.ProcessPath is unavailable.");
        }

        string[] currentArgs = Environment.GetCommandLineArgs();

        var info =
            new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        bool launchedByDotnet =
            Path.GetFileNameWithoutExtension(processPath)
                .Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        if (launchedByDotnet)
        {
            info.FileName = processPath;

            if (currentArgs.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine current managed assembly path.");
            }

            info.ArgumentList.Add(currentArgs[0]);
        }
        else
        {
            info.FileName = processPath;
        }

        info.ArgumentList.Add("--injector-worker");
        info.ArgumentList.Add("--sink");
        info.ArgumentList.Add(sinkMatch);
        info.ArgumentList.Add("--ready-event");
        info.ArgumentList.Add(readyEventName);
        info.ArgumentList.Add("--start-event");
        info.ArgumentList.Add(startEventName);
        info.ArgumentList.Add("--result-file");
        info.ArgumentList.Add(resultPath);
        info.ArgumentList.Add("--runs");
        info.ArgumentList.Add(runs.ToString(CultureInfo.InvariantCulture));

        return
            Process.Start(info)
            ?? throw new InvalidOperationException(
                "Failed to start injector child.");
    }

    public static string ReadDiagnostics(Process child)
    {
        try
        {
            string stdout = child.StandardOutput.ReadToEnd();
            string stderr = child.StandardError.ReadToEnd();
            string combined = (stdout + Environment.NewLine + stderr).Trim();

            return combined.Length == 0
                ? "(no child diagnostics)"
                : combined;
        }
        catch
        {
            return "(child diagnostics unavailable)";
        }
    }

    public static InjectorTiming ReadTiming(
        string resultPath,
        int expectedRuns)
    {
        if (!File.Exists(resultPath))
        {
            throw new FileNotFoundException(
                "Injector timing result file was not created.",
                resultPath);
        }

        string[] lines = File.ReadAllLines(resultPath);

        if (lines.Length != expectedRuns + 1)
        {
            throw new InvalidOperationException(
                $"Injector timing file contains {lines.Length} lines; " +
                $"expected {expectedRuns + 1}.");
        }

        double coldStart =
            double.Parse(
                lines[0],
                CultureInfo.InvariantCulture);

        var chirps = new long[expectedRuns];

        for (int i = 0; i < expectedRuns; i++)
        {
            chirps[i] =
                long.Parse(
                    lines[i + 1],
                    CultureInfo.InvariantCulture);
        }

        return new InjectorTiming(coldStart, chirps);
    }
}
