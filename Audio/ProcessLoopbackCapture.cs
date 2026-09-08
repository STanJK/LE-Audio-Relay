using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Audio;

internal delegate void ProcessLoopbackPacketHandler(
    ReadOnlySpan<byte> buffer,
    AudioClientBufferFlags flags,
    long devicePosition,
    long qpcPosition);

[SupportedOSPlatform("windows")]
internal sealed class ProcessLoopbackCapture : IDisposable
{
    private readonly WasapiRecorder _recorder;
    private bool _started;

    public event ProcessLoopbackPacketHandler? Packet;

    private ProcessLoopbackCapture(WasapiRecorder recorder)
    {
        _recorder = recorder;

        _recorder.DataAvailable +=
            (buffer, flags, devicePosition, qpcPosition) =>
                Packet?.Invoke(buffer, flags, devicePosition, qpcPosition);
    }

    public static ProcessLoopbackCapture CreateExcludeProcessTree(
        uint processId,
        WaveFormat format,
        int bufferMs)
    {
#pragma warning disable CA1416
        WasapiRecorder recorder =
            Task.Run(() =>
                    new WasapiRecorderBuilder()
                        .WithProcessLoopback(
                            processId,
                            ProcessLoopbackMode.ExcludeTargetProcessTree)
                        .WithFormat(format)
                        .WithSharedMode()
                        .WithEventSync()
                        .WithBufferLength(bufferMs)
                        .WithMmcssThreadPriority("Pro Audio")
                        .BuildAsync())
                .GetAwaiter()
                .GetResult();
#pragma warning restore CA1416

        return new ProcessLoopbackCapture(recorder);
    }

    public static ProcessLoopbackCapture CreateIncludeProcessTree(
        uint processId,
        WaveFormat format,
        int bufferMs)
    {
#pragma warning disable CA1416
        WasapiRecorder recorder =
            Task.Run(() =>
                    new WasapiRecorderBuilder()
                        .WithProcessLoopback(
                            processId,
                            ProcessLoopbackMode.IncludeTargetProcessTree)
                        .WithFormat(format)
                        .WithSharedMode()
                        .WithEventSync()
                        .WithBufferLength(bufferMs)
                        .WithMmcssThreadPriority("Pro Audio")
                        .BuildAsync())
                .GetAwaiter()
                .GetResult();
#pragma warning restore CA1416

        return new ProcessLoopbackCapture(recorder);
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _recorder.StartRecording();
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _recorder.StopRecording();
        _started = false;
    }

    public void Dispose()
    {
        try
        {
            Stop();
        }
        catch
        {
        }

        _recorder.Dispose();
    }
}
