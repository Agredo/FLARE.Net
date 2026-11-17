namespace Flare.Models.Analysis;

public enum HighlightNormalization
{
    /// <summary>Min-max over the whole matrix, as in the original FLARE app.</summary>
    GlobalMinMax,

    /// <summary>Min-max within each peak row: best atom of every peak = 1.</summary>
    PeakMinMax,

    /// <summary>Softmax over the atoms of each peak (temperature-scaled), divided by the global maximum.</summary>
    Softmax,
}

/// <summary>Turns the raw cosine similarity matrix into highlight weights in [0, 1].</summary>
public static class HighlightCalculator
{
    public const double DefaultTemperature = 0.05;

    /// <summary>Normalised copy of the [peaks, atoms] matrix, values in [0, 1].</summary>
    public static float[] Normalize(AnalysisResult result, HighlightNormalization mode, double temperature = DefaultTemperature) =>
        Normalize(result.Similarity, result.NumPeaks, result.NumAtoms, mode, temperature);

    public static float[] Normalize(float[] similarity, int peaks, int atoms, HighlightNormalization mode,
        double temperature = DefaultTemperature)
    {
        if (similarity.Length != peaks * atoms) throw new ArgumentException("matrix size does not match peaks * atoms");
        var w = new float[similarity.Length];
        if (w.Length == 0) return w;

        switch (mode)
        {
            case HighlightNormalization.GlobalMinMax:
                MinMax(similarity, w, 0, w.Length);
                break;
            case HighlightNormalization.PeakMinMax:
                for (int p = 0; p < peaks; p++) MinMax(similarity, w, p * atoms, atoms);
                break;
            case HighlightNormalization.Softmax:
                double t = Math.Max(temperature, 1e-6);
                for (int p = 0; p < peaks; p++)
                {
                    int o = p * atoms;
                    float max = float.MinValue;
                    for (int a = 0; a < atoms; a++) max = Math.Max(max, similarity[o + a]);
                    double sum = 0;
                    for (int a = 0; a < atoms; a++) sum += w[o + a] = (float)Math.Exp((similarity[o + a] - max) / t);
                    for (int a = 0; a < atoms; a++) w[o + a] = (float)(w[o + a] / sum);
                }
                float globalMax = w.Max();
                if (globalMax > 0) for (int i = 0; i < w.Length; i++) w[i] /= globalMax;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
        return w;
    }

    /// <summary>Row of <paramref name="peak"/>, or the per-atom maximum over all peaks when null.</summary>
    public static float[] AtomWeights(float[] normalized, int peaks, int atoms, int? peak)
    {
        var w = new float[atoms];
        if (peak is int p)
        {
            Array.Copy(normalized, p * atoms, w, 0, atoms);
            return w;
        }
        for (int i = 0; i < peaks; i++)
            for (int a = 0; a < atoms; a++)
                w[a] = Math.Max(w[a], normalized[i * atoms + a]);
        return w;
    }

    /// <summary>Column of <paramref name="atom"/>, or the per-peak maximum over all atoms when null.</summary>
    public static float[] PeakWeights(float[] normalized, int peaks, int atoms, int? atom)
    {
        var w = new float[peaks];
        for (int p = 0; p < peaks; p++)
        {
            if (atom is int a)
            {
                w[p] = normalized[p * atoms + a];
                continue;
            }
            for (int k = 0; k < atoms; k++) w[p] = Math.Max(w[p], normalized[p * atoms + k]);
        }
        return w;
    }

    private static void MinMax(float[] src, float[] dst, int offset, int count)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = offset; i < offset + count; i++)
        {
            min = Math.Min(min, src[i]);
            max = Math.Max(max, src[i]);
        }
        float range = max - min;
        for (int i = offset; i < offset + count; i++)
            dst[i] = range > 1e-12f ? (src[i] - min) / range : 1f;
    }
}
