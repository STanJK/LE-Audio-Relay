namespace LEKeepAliveRelay.Diagnostics.Latency;

internal static class CorrelationAnalyzer
{
    private const int Decimation = 4;

    public static CorrelationHit FindTimed(
        CaptureSnapshot snapshot,
        float[] reference,
        long searchStartQpc,
        long searchEndQpc)
    {
        int searchStartFrame =
            snapshot.FindFrameAtOrAfterQpc(searchStartQpc);

        int searchEndFrame =
            snapshot.FindFrameAtOrAfterQpc(searchEndQpc);

        if (searchEndFrame <= searchStartFrame)
        {
            searchEndFrame = snapshot.Samples.Length;
        }

        CorrelationResult result =
            FindBest(
                snapshot.Samples,
                reference,
                searchStartFrame,
                searchEndFrame);

        return new CorrelationHit(
            snapshot.QpcForFrame(result.StartFrame),
            result.Score);
    }

    private static CorrelationResult FindBest(
        float[] captured,
        float[] reference,
        int searchStart,
        int searchEnd)
    {
        int maxStart =
            Math.Min(searchEnd, captured.Length) - reference.Length;

        if (maxStart <= searchStart)
        {
            throw new InvalidOperationException(
                "Correlation search window is too short.");
        }

        double referenceEnergy = 0;

        for (int j = 0; j < reference.Length; j += Decimation)
        {
            double x = reference[j];
            referenceEnergy += x * x;
        }

        double bestScore = double.NegativeInfinity;
        int bestFrame = -1;

        for (int start = Math.Max(0, searchStart);
             start <= maxStart;
             start += Decimation)
        {
            double xy = 0;
            double yy = 0;

            for (int j = 0; j < reference.Length; j += Decimation)
            {
                double x = reference[j];
                double y = captured[start + j];

                xy += x * y;
                yy += y * y;
            }

            if (yy <= 1e-12)
            {
                continue;
            }

            double score =
                Math.Abs(xy) /
                Math.Sqrt(referenceEnergy * yy);

            if (score > bestScore)
            {
                bestScore = score;
                bestFrame = start;
            }
        }

        if (bestFrame < 0)
        {
            throw new InvalidOperationException(
                "No correlation peak found.");
        }

        return new CorrelationResult(bestFrame, bestScore);
    }
}
