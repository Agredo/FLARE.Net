namespace Flare.Onnx;

public readonly record struct Peak(double Mz, double Intensity);

/// <summary>Port of spectra_utils.process_spec_file / max_inten_spec (MIST preprocessing).</summary>
public static class SpectrumPreprocessor
{
    public const int DefaultPrecision = 4;
    public const double DefaultMinIntensity = 0.001;
    public const int DefaultMaxPeaks = 60;

    /// <summary>
    /// Merges peaks by m/z rounded to <paramref name="precision"/> decimals (max intensity, first m/z kept),
    /// drops peaks above parent mass + 1, normalises to max 1, takes sqrt, keeps the
    /// <paramref name="maxPeaks"/> most intense peaks with intensity &gt; <paramref name="minIntensity"/>.
    /// Output is ordered by decreasing intensity. Returns an empty list when nothing is left.
    /// </summary>
    public static List<Peak> Process(
        IReadOnlyList<double> mzs,
        IReadOnlyList<double> intensities,
        double parentMass,
        int precision = DefaultPrecision,
        double minIntensity = DefaultMinIntensity,
        int maxPeaks = DefaultMaxPeaks)
    {
        if (mzs.Count != intensities.Count) throw new ArgumentException("mzs and intensities differ in length");

        double scale = Math.Pow(10, precision);
        var index = new Dictionary<double, int>();
        var merged = new List<double[]>();
        for (int i = 0; i < mzs.Count; i++)
        {
            double key = Math.Round(mzs[i] * scale, MidpointRounding.ToEven) / scale;
            if (!index.TryGetValue(key, out var j))
            {
                index[key] = merged.Count;
                merged.Add([mzs[i], intensities[i]]);
            }
            else if (intensities[i] > merged[j][1])
            {
                merged[j][1] = intensities[i];
            }
        }

        var kept = merged.Where(p => p[0] <= parentMass + 1).ToList();
        if (kept.Count == 0) return [];
        double max = kept.Max(p => p[1]);
        var peaks = kept.Select(p => new Peak(p[0], Math.Sqrt(p[1] / max))).ToArray();

        // np.argsort(intens)[::-1] (descending). numpy's sort is not stable, so the order of
        // equal intensities is implementation-defined; we keep the input order for ties.
        // The model is permutation invariant; only ties exactly at the max_peaks cut-off can differ.
        var order = Enumerable.Range(0, peaks.Length)
            .OrderByDescending(i => peaks[i].Intensity)
            .Take(maxPeaks);
        return order.Select(i => peaks[i]).Where(p => p.Intensity > minIntensity).ToList();
    }
}
