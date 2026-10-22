using System.Runtime.InteropServices;

namespace LEAudioRelay.DSP;

// Experimental fixed 48 kHz stereo PEQ. No allocations or locks on the render callback.
internal sealed class RelayEqualizer
{
    private const double SampleRate = 48000.0;
    private readonly Section[] _sections;
    private readonly float _gain;

    private RelayEqualizer(float gain, Section[] sections)
    {
        _gain = gain;
        _sections = sections;
    }

    public static RelayEqualizer? FromEnvironment()
    {
        string preset = (Environment.GetEnvironmentVariable("LERELAY_PEQ") ?? "off")
            .Trim().ToLowerInvariant();
        if (preset is "" or "off" or "bypass") return null;
        if (preset is "diagnostic" or "test")
            return new RelayEqualizer((float)Math.Pow(10, -30.0 / 20), Array.Empty<Section>());

        var bands = preset switch
        {
            "reference" => new (bool shelf, double freq, double gain, double q)[]
            {
                (false, 140, -1.0, 0.75), (false, 280, -0.8, 0.9),
                (false, 3000, 0.4, 0.9), (true, 8000, 0.6, 0.7)
            },
            "showcase" or "two-way" => new (bool shelf, double freq, double gain, double q)[]
            {
                (false, 120, -1.5, 0.70), (false, 220, -1.5, 1.0),
                (false, 2800, 0.8, 0.8), (false, 5500, 0.7, 1.0),
                (true, 7500, 1.2, 0.70)
            },
            "kino" or "rock" => new (bool shelf, double freq, double gain, double q)[]
            {
                (false, 110, 0.8, 0.8), (false, 250, -2.0, 0.85),
                (false, 1300, 0.7, 0.9), (false, 3500, 1.1, 0.9),
                (true, 7900, 0.7, 0.7)
            },
            _ => throw new ArgumentException(
                $"Unknown LERELAY_PEQ preset '{preset}'. Use off/reference/showcase/kino/diagnostic.")
        };
        var sections = new Section[bands.Length];
        for (int i = 0; i < bands.Length; i++)
        {
            var b = bands[i];
            sections[i] = b.shelf
                ? Section.HighShelf(b.freq, b.gain, b.q)
                : Section.Peaking(b.freq, b.gain, b.q);
        }
        return new RelayEqualizer((float)Math.Pow(10, -3.0 / 20), sections);
    }

    public void Process(Span<byte> pcm)
    {
        // The route format policy guarantees stereo float32 interleaved PCM.
        Span<float> samples = MemoryMarshal.Cast<byte, float>(pcm);
        for (int i = 0; i + 1 < samples.Length; i += 2)
        {
            double left = samples[i] * _gain;
            double right = samples[i + 1] * _gain;
            foreach (Section s in _sections)
            {
                left = s.Run(left, false);
                right = s.Run(right, true);
            }
            samples[i] = (float)left;
            samples[i + 1] = (float)right;
        }
    }

    private sealed class Section
    {
        private readonly double b0, b1, b2, a1, a2;
        private double l1, l2, r1, r2;

        private Section(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0;
            this.a1 = a1 / a0; this.a2 = a2 / a0;
        }

        public double Run(double x, bool right)
        {
            // Transposed Direct Form II; independent state for each ear.
            if (right)
            {
                double y = b0 * x + r1;
                r1 = b1 * x - a1 * y + r2;
                r2 = b2 * x - a2 * y;
                return y;
            }
            else
            {
                double y = b0 * x + l1;
                l1 = b1 * x - a1 * y + l2;
                l2 = b2 * x - a2 * y;
                return y;
            }
        }

        public static Section Peaking(double fc, double db, double q)
        {
            double A = Math.Pow(10, db / 40);
            double w = 2 * Math.PI * fc / SampleRate;
            double alpha = Math.Sin(w) / (2 * q);
            double c = Math.Cos(w);
            return new Section(1 + alpha * A, -2 * c, 1 - alpha * A,
                1 + alpha / A, -2 * c, 1 - alpha / A);
        }

        public static Section HighShelf(double fc, double db, double q)
        {
            // RBJ high shelf; caller's Q is interpreted as the cookbook slope S.
            double A = Math.Pow(10, db / 40);
            double w = 2 * Math.PI * fc / SampleRate;
            double c = Math.Cos(w);
            double alpha = Math.Sin(w) / 2 *
                Math.Sqrt((A + 1 / A) * (1 / q - 1) + 2);
            double root = 2 * Math.Sqrt(A) * alpha;
            return new Section(
                A * ((A + 1) + (A - 1) * c + root),
                -2 * A * ((A - 1) + (A + 1) * c),
                A * ((A + 1) + (A - 1) * c - root),
                (A + 1) - (A - 1) * c + root,
                2 * ((A - 1) - (A + 1) * c),
                (A + 1) - (A - 1) * c - root);
        }
    }
}
