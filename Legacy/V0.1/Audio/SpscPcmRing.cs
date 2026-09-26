namespace LEKeepAliveRelay.Audio;

internal sealed class SpscPcmRing
{
    private readonly byte[] _buffer;
    private readonly int _capacityFrames;
    private readonly int _blockAlign;

    private long _writeFrames;
    private long _readFrames;

    private long _overflowFrames;
    private long _underrunFrames;
    private long _startupDroppedIncomingFrames;

    public SpscPcmRing(
        int capacityFrames,
        int blockAlign)
    {
        if (capacityFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityFrames));
        }

        if (blockAlign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockAlign));
        }

        _capacityFrames =
            capacityFrames;

        _blockAlign =
            blockAlign;

        _buffer =
            new byte[
                capacityFrames *
                blockAlign];
    }

    public int BlockAlign =>
        _blockAlign;

    public int FillFrames
    {
        get
        {
            long write =
                Volatile.Read(
                    ref _writeFrames);

            long read =
                Volatile.Read(
                    ref _readFrames);

            long fill =
                write -
                read;

            if (fill <= 0)
            {
                return 0;
            }

            if (fill >=
                _capacityFrames)
            {
                return
                    _capacityFrames;
            }

            return
                (int)fill;
        }
    }

    public long OverflowFrames =>
        Interlocked.Read(
            ref _overflowFrames);

    public long UnderrunFrames =>
        Interlocked.Read(
            ref _underrunFrames);

    public long StartupDroppedIncomingFrames =>
        Interlocked.Read(
            ref _startupDroppedIncomingFrames);

    public int WriteRuntime(
        ReadOnlySpan<byte> source)
    {
        return WriteInternal(
            source,
            maxFillFrames:
                _capacityFrames,
            countRuntimeOverflow:
                true);
    }

    public int WriteZerosRuntime(
        int inputFrames)
    {
        return WriteZerosInternal(
            inputFrames,
            maxFillFrames:
                _capacityFrames,
            countRuntimeOverflow:
                true);
    }

    public int WriteStartup(
        ReadOnlySpan<byte> source,
        int maxFillFrames)
    {
        return WriteInternal(
            source,
            maxFillFrames,
            countRuntimeOverflow:
                false);
    }

    public int WriteZerosStartup(
        int inputFrames,
        int maxFillFrames)
    {
        return WriteZerosInternal(
            inputFrames,
            maxFillFrames,
            countRuntimeOverflow:
                false);
    }

    public int ReadRuntime(
        Span<byte> destination,
        int requestedFrames)
    {
        if (requestedFrames <= 0)
        {
            return 0;
        }

        int destinationFrames =
            destination.Length /
            _blockAlign;

        requestedFrames =
            Math.Min(
                requestedFrames,
                destinationFrames);

        long read =
            _readFrames;

        long write =
            Volatile.Read(
                ref _writeFrames);

        long available =
            write -
            read;

        int framesToRead =
            available <= 0
                ? 0
                : Math.Min(
                    requestedFrames,
                    (int)Math.Min(
                        available,
                        int.MaxValue));

        if (framesToRead > 0)
        {
            CopyFromRing(
                destination,
                framesToRead,
                read);

            Volatile.Write(
                ref _readFrames,
                read +
                framesToRead);
        }

        int missing =
            requestedFrames -
            framesToRead;

        if (missing > 0)
        {
            Interlocked.Add(
                ref _underrunFrames,
                missing);
        }

        return
            framesToRead;
    }

    public int Discard(
        int requestedFrames)
    {
        if (requestedFrames <= 0)
        {
            return 0;
        }

        long read =
            _readFrames;

        long write =
            Volatile.Read(
                ref _writeFrames);

        long available =
            write -
            read;

        int frames =
            available <= 0
                ? 0
                : Math.Min(
                    requestedFrames,
                    (int)Math.Min(
                        available,
                        int.MaxValue));

        if (frames > 0)
        {
            Volatile.Write(
                ref _readFrames,
                read +
                frames);
        }

        return frames;
    }

    private int WriteInternal(
        ReadOnlySpan<byte> source,
        int maxFillFrames,
        bool countRuntimeOverflow)
    {
        int inputFrames =
            source.Length /
            _blockAlign;

        if (inputFrames <= 0)
        {
            return 0;
        }

        maxFillFrames =
            Math.Clamp(
                maxFillFrames,
                1,
                _capacityFrames);

        long write =
            _writeFrames;

        long read =
            Volatile.Read(
                ref _readFrames);

        long used =
            write -
            read;

        int freeToLimit =
            used >= maxFillFrames
                ? 0
                : maxFillFrames -
                  (int)used;

        int framesToWrite =
            Math.Min(
                inputFrames,
                freeToLimit);

        if (framesToWrite > 0)
        {
            CopyIntoRing(
                source,
                framesToWrite,
                write);

            Volatile.Write(
                ref _writeFrames,
                write +
                framesToWrite);
        }

        int dropped =
            inputFrames -
            framesToWrite;

        if (dropped > 0)
        {
            if (countRuntimeOverflow)
            {
                Interlocked.Add(
                    ref _overflowFrames,
                    dropped);
            }
            else
            {
                Interlocked.Add(
                    ref _startupDroppedIncomingFrames,
                    dropped);
            }
        }

        return framesToWrite;
    }

    private int WriteZerosInternal(
        int inputFrames,
        int maxFillFrames,
        bool countRuntimeOverflow)
    {
        if (inputFrames <= 0)
        {
            return 0;
        }

        maxFillFrames =
            Math.Clamp(
                maxFillFrames,
                1,
                _capacityFrames);

        long write =
            _writeFrames;

        long read =
            Volatile.Read(
                ref _readFrames);

        long used =
            write -
            read;

        int freeToLimit =
            used >= maxFillFrames
                ? 0
                : maxFillFrames -
                  (int)used;

        int framesToWrite =
            Math.Min(
                inputFrames,
                freeToLimit);

        if (framesToWrite > 0)
        {
            ClearRingRegion(
                framesToWrite,
                write);

            Volatile.Write(
                ref _writeFrames,
                write +
                framesToWrite);
        }

        int dropped =
            inputFrames -
            framesToWrite;

        if (dropped > 0)
        {
            if (countRuntimeOverflow)
            {
                Interlocked.Add(
                    ref _overflowFrames,
                    dropped);
            }
            else
            {
                Interlocked.Add(
                    ref _startupDroppedIncomingFrames,
                    dropped);
            }
        }

        return framesToWrite;
    }

    private void CopyIntoRing(
        ReadOnlySpan<byte> source,
        int frames,
        long writeFrame)
    {
        int startFrame =
            (int)(
                writeFrame %
                _capacityFrames);

        int firstFrames =
            Math.Min(
                frames,
                _capacityFrames -
                startFrame);

        int firstBytes =
            firstFrames *
            _blockAlign;

        source
            .Slice(
                0,
                firstBytes)
            .CopyTo(
                _buffer.AsSpan(
                    startFrame *
                    _blockAlign,
                    firstBytes));

        int remainingFrames =
            frames -
            firstFrames;

        if (remainingFrames <= 0)
        {
            return;
        }

        int remainingBytes =
            remainingFrames *
            _blockAlign;

        source
            .Slice(
                firstBytes,
                remainingBytes)
            .CopyTo(
                _buffer.AsSpan(
                    0,
                    remainingBytes));
    }

    private void ClearRingRegion(
        int frames,
        long writeFrame)
    {
        int startFrame =
            (int)(
                writeFrame %
                _capacityFrames);

        int firstFrames =
            Math.Min(
                frames,
                _capacityFrames -
                startFrame);

        _buffer
            .AsSpan(
                startFrame *
                _blockAlign,
                firstFrames *
                _blockAlign)
            .Clear();

        int remainingFrames =
            frames -
            firstFrames;

        if (remainingFrames > 0)
        {
            _buffer
                .AsSpan(
                    0,
                    remainingFrames *
                    _blockAlign)
                .Clear();
        }
    }

    private void CopyFromRing(
        Span<byte> destination,
        int frames,
        long readFrame)
    {
        int startFrame =
            (int)(
                readFrame %
                _capacityFrames);

        int firstFrames =
            Math.Min(
                frames,
                _capacityFrames -
                startFrame);

        int firstBytes =
            firstFrames *
            _blockAlign;

        _buffer
            .AsSpan(
                startFrame *
                _blockAlign,
                firstBytes)
            .CopyTo(
                destination.Slice(
                    0,
                    firstBytes));

        int remainingFrames =
            frames -
            firstFrames;

        if (remainingFrames <= 0)
        {
            return;
        }

        int remainingBytes =
            remainingFrames *
            _blockAlign;

        _buffer
            .AsSpan(
                0,
                remainingBytes)
            .CopyTo(
                destination.Slice(
                    firstBytes,
                    remainingBytes));
    }
}
