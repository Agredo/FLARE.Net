using Flare.Chem;
namespace Flare.Onnx;

/// <summary>
/// Builds the FLARE spectrum input [P, 15]: precursor handling of Subformula_Loader.load_mist_data
/// followed by SpecFormulaFeaturizer (element counts / MSGYM_FORMULA_VECTOR_NORM + intensity).
/// </summary>
public static class SpectrumFeaturizer
{
    public const int NumFeatures = 15;
    public const double DefaultMaxMz = 1000;

    private static readonly Dictionary<string, int> ElementPos =
        ChemUtils.FeatureElements.Select((e, i) => (e, i)).ToDictionary(t => t.e, t => t.i);

    /// <summary>
    /// Adds the precursor peak (intensity 1.1) if the neutral formula was not assigned,
    /// otherwise sets the intensity of its first occurrence to 1.1; then sorts by m/z.
    /// </summary>
    public static List<FormulaPeak> AddPrecursor(IReadOnlyList<FormulaPeak> assigned, string formula, double precursorMz)
    {
        var peaks = assigned.ToList();
        int idx = peaks.FindIndex(p => p.Formula == formula);
        if (idx < 0) peaks.Add(new FormulaPeak(precursorMz, ChemUtils.PrecursorIntensity, formula));
        else peaks[idx] = peaks[idx] with { Intensity = ChemUtils.PrecursorIntensity };
        return peaks.OrderBy(p => p.Mz).ToList();
    }

    /// <summary>Row-major float[P * 15] for peaks with m/z &lt;= <paramref name="maxMz"/>.</summary>
    public static float[] Featurize(IReadOnlyList<FormulaPeak> peaks, out int numPeaks, double maxMz = DefaultMaxMz)
    {
        var kept = peaks.Where(p => p.Mz <= maxMz).ToList();
        numPeaks = kept.Count;
        var x = new float[kept.Count * NumFeatures];
        var counts = new double[ChemUtils.FeatureElements.Length];
        for (int i = 0; i < kept.Count; i++)
        {
            Array.Clear(counts);
            foreach (var (el, ct) in ChemUtils.ParseFormula(kept[i].Formula))
                if (ElementPos.TryGetValue(el, out var e)) counts[e] += ct;
            for (int e = 0; e < counts.Length; e++)
                x[i * NumFeatures + e] = (float)(counts[e] / ChemUtils.FormulaNormVector[e]);
            x[i * NumFeatures + NumFeatures - 1] = (float)kept[i].Intensity;
        }
        return x;
    }
}
