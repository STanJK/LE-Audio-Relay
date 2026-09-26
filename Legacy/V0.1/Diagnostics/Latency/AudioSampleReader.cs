using System.Buffers.Binary;
using NAudio.Wave;

namespace LEKeepAliveRelay.Diagnostics.Latency;

internal static class AudioSampleReader
{
    private static readonly Guid IeeeFloatSubFormat =
        new("00000003-0000-0010-8000-00AA00389B71");

    private static readonly Guid PcmSubFormat =
        new("00000001-0000-0010-8000-00AA00389B71");

    public static void EnsureSupported(WaveFormat format)
    {
        if (IsFloat32(format))
        {
            return;
        }

        if (IsPcm(format) && format.BitsPerSample is 16 or 24 or 32)
        {
            return;
        }

        throw new NotSupportedException(
            $"Capture format is not supported: {format}");
    }

    public static float ReadChannelZero(
        ReadOnlySpan<byte> buffer,
        int offset,
        WaveFormat format)
    {
        if (IsFloat32(format))
        {
            int raw =
                BinaryPrimitives.ReadInt32LittleEndian(
                    buffer.Slice(offset, 4));

            return BitConverter.Int32BitsToSingle(raw);
        }

        return format.BitsPerSample switch
        {
            16 =>
                BinaryPrimitives.ReadInt16LittleEndian(
                    buffer.Slice(offset, 2)) / 32768f,

            24 =>
                ReadPcm24(buffer.Slice(offset, 3)) / 8388608f,

            32 =>
                BinaryPrimitives.ReadInt32LittleEndian(
                    buffer.Slice(offset, 4)) / 2147483648f,

            _ =>
                throw new NotSupportedException(
                    $"Unsupported capture format: {format}")
        };
    }

    private static int ReadPcm24(ReadOnlySpan<byte> sample)
    {
        int value =
            sample[0] |
            (sample[1] << 8) |
            (sample[2] << 16);

        if ((value & 0x00800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    private static bool IsFloat32(WaveFormat format)
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

    private static bool IsPcm(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            return true;
        }

        return format is WaveFormatExtensible ext &&
               ext.SubFormat == PcmSubFormat;
    }
}
