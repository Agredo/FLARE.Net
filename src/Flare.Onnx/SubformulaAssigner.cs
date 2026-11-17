using Flare.Chem;
namespace Flare.Onnx;

public readonly record struct FormulaPeak(double Mz, double Intensity, string Formula);

/// <summary>Port of spectra_utils.assign_subforms (MIST subformula assignment).</summary>
public sealed class SubformulaAssigner
{
    public const double DefaultMassDiffThresholdPpm = 20;

    private static readonly double[] RdbeMult = BuildRdbeMult();

    private readonly int[][] _subsets;
    private readonly double[] _massesWithIon;

    public string Formula { get; }
    public string Adduct { get; }
    public int SubsetCount => _subsets.Length;

    /// <param name="formula">Neutral molecular formula of the candidate/precursor.</param>
    /// <param name="adduct">Adduct, e.g. "[M+H]+" (aliases such as "M+H" are accepted).</param>
    public SubformulaAssigner(string formula, string adduct)
    {
        Formula = formula;
        Adduct = ChemUtils.StandardizeAdduct(adduct);
        _subsets = EnumerateSubsets(ChemUtils.FormulaToDense(formula));
        double ionMass = ChemUtils.IonToMass[Adduct];
        _massesWithIon = new double[_subsets.Length];
        for (int i = 0; i < _subsets.Length; i++)
        {
            double m = 0;
            for (int e = 0; e < ChemUtils.ValidElements.Length; e++) m += _subsets[i][e] * ChemUtils.ValidMonoMasses[e];
            _massesWithIon[i] = m + ionMass;
        }
    }

    /// <summary>
    /// Assigns to each peak the closest subformula (+ ion) and keeps peaks within the ppm threshold
    /// (ppm denominator clipped to &gt;= 200). Order of <paramref name="peaks"/> is preserved.
    /// Like the original, duplicate formulas are NOT merged (the de-duplication dict there is never filled).
    /// </summary>
    public List<FormulaPeak> Assign(IReadOnlyList<Peak> peaks, double massDiffThresholdPpm = DefaultMassDiffThresholdPpm)
    {
        var result = new List<FormulaPeak>();
        foreach (var p in peaks)
        {
            int best = 0;
            double bestDiff = double.PositiveInfinity;
            for (int i = 0; i < _massesWithIon.Length; i++)
            {
                double d = Math.Abs(p.Mz - _massesWithIon[i]);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            double ppm = bestDiff / (p.Mz < 200 ? 200 : p.Mz) * 1e6;
            if (ppm < massDiffThresholdPpm)
                result.Add(new FormulaPeak(p.Mz, p.Intensity, ChemUtils.VecToFormula(_subsets[best])));
        }
        return result;
    }

    // Same order as reduce(cross_sum, ...): first (VALID_ELEMENTS order) element varies fastest.
    private static int[][] EnumerateSubsets(int[] dense)
    {
        var nonZero = Enumerable.Range(0, dense.Length).Where(i => dense[i] > 0).ToArray();
        long total = 1;
        foreach (var i in nonZero) total *= dense[i] + 1;
        if (total > 10_000_000) throw new ArgumentException($"Formula too large for subformula enumeration ({total} subsets)");

        var list = new List<int[]>();
        var counter = new int[nonZero.Length];
        for (long n = 0; n < total; n++)
        {
            var v = new int[dense.Length];
            double rdbe = 0;
            for (int k = 0; k < nonZero.Length; k++)
            {
                v[nonZero[k]] = counter[k];
                rdbe += counter[k] * RdbeMult[nonZero[k]];
            }
            if (1 + 0.5 * rdbe >= 0) list.Add(v);
            for (int k = 0; k < counter.Length; k++)
            {
                if (++counter[k] <= dense[nonZero[k]]) break;
                counter[k] = 0;
            }
        }
        return list.ToArray();
    }

    private static double[] BuildRdbeMult()
    {
        var mult = new double[ChemUtils.ValidElements.Length];
        string[] els = ["C", "N", "P", "H", "Cl", "Br", "I", "F"];
        double[] w = [2, 1, 1, -1, -1, -1, -1, -1];
        for (int i = 0; i < els.Length; i++) mult[Array.IndexOf(ChemUtils.ValidElements, els[i])] = w[i];
        return mult;
    }
}
