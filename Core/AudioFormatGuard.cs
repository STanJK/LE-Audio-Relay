using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LEKeepAliveRelay.Core;

internal static class AudioFormatGuard
{
    private static readonly Guid IeeeFloatSubFormat =
        new("00000003-0000-0010-8000-00AA00389B71");

    public static void Ensure48kFloatStereo(MMDevice device)
    {
        using var client = device.CreateAudioClient();
        WaveFormat mix = client.MixFormat;

        bool float32 =
            mix.Encoding == WaveFormatEncoding.IeeeFloat &&
            mix.BitsPerSample == 32;

        if (mix is WaveFormatExtensible ext)
        {
            float32 =
                ext.SubFormat == IeeeFloatSubFormat &&
                mix.BitsPerSample == 32;
        }

        if (mix.SampleRate != 48_000 ||
            mix.Channels != 2 ||
            !float32)
        {
            throw new InvalidOperationException(
                "Endpoint must expose 48 kHz Float32 Stereo MixFormat. " +
                $"Actual: {mix}");
        }
    }

    public static bool IsFloat32(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat &&
            format.BitsPerSample == 32)
        {
            return true;
        }

        return format is WaveFormatExtensible ext &&
               ext.SubFormat == IeeeFloatSubFormat &&
               format.BitsPerSample == 32;
    }
}
