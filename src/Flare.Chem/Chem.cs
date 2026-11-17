using System.Text;
using System.Text.RegularExpressions;

namespace Flare.Chem;

/// <summary>Chemistry constants and formula helpers (port of flare/subformula_assign/utils/chem_utils.py).</summary>
public static partial class ChemUtils
{
    public const double ElectronMass = 0.00054858;

    /// <summary>Element order used for subformula enumeration (MIST VALID_ELEMENTS).</summary>
    public static readonly string[] ValidElements =
        ["C", "H", "As", "B", "Br", "Cl", "Co", "F", "Fe", "I", "K", "N", "Na", "O", "P", "S", "Se", "Si"];

    /// <summary>Most common isotope masses, identical to RDKit's PeriodicTable.GetMostCommonIsotopeMass.</summary>
    public static readonly double[] ValidMonoMasses =
    [
        12.0, 1.007825032, 74.9215965, 11.0093054, 78.9183371, 34.96885268, 58.933195, 18.99840322,
        55.9349375, 126.904473, 38.96370668, 14.003074, 22.98976928, 15.99491462, 30.97376163,
        31.972071, 79.9165213, 27.97692653,
    ];

    /// <summary>Elements of the FLARE spectrum featurisation (CHEM_ELEMS_SMALL / ckpt element_list).</summary>
    public static readonly string[] FeatureElements =
        ["H", "C", "O", "N", "P", "S", "Cl", "F", "Br", "I", "B", "As", "Si", "Se"];

    /// <summary>MSGYM_FORMULA_VECTOR_NORM.</summary>
    public static readonly double[] FormulaNormVector = [102, 59, 25, 13, 3, 6, 6, 17, 4, 4, 1, 1, 5, 2];

    public const double PrecursorIntensity = 1.1;

    private static readonly Dictionary<string, int> ValidIndex =
        ValidElements.Select((e, i) => (e, i)).ToDictionary(t => t.e, t => t.i);

    private static double M(string el) => ValidMonoMasses[ValidIndex[el]];

    public static readonly IReadOnlyDictionary<string, double> IonToMass = new Dictionary<string, double>
    {
        ["[M+H]+"] = M("H") - ElectronMass,
        ["[M+Na]+"] = M("Na") - ElectronMass,
        ["[M+K]+"] = M("K") - ElectronMass,
        ["[M-H2O+H]+"] = -M("O") - M("H") - ElectronMass,
        ["[M+H3N+H]+"] = M("N") + M("H") * 4 - ElectronMass,
        ["[M]+"] = 0 - ElectronMass,
        ["[M-H4O2+H]+"] = -M("O") * 2 - M("H") * 3 - ElectronMass,
        ["[M-H]-"] = M("H") + ElectronMass,
    };

    private static readonly Dictionary<string, string> IonRemap = new()
    {
        ["[M+NH4]+"] = "[M+H3N+H]+",
        ["M+H"] = "[M+H]+",
        ["M+Na"] = "[M+Na]+",
        ["M+H-H2O"] = "[M-H2O+H]+",
        ["M-H2O+H"] = "[M-H2O+H]+",
        ["M+NH4"] = "[M+H3N+H]+",
        ["M-2H2O+H"] = "[M-H4O2+H]+",
        ["[M-2H2O+H]+"] = "[M-H4O2+H]+",
    };

    /// <summary>Maps adduct aliases to the canonical MIST ion name; throws for unsupported adducts.</summary>
    public static string StandardizeAdduct(string adduct)
    {
        var a = adduct.Replace(" ", "");
        if (IonRemap.TryGetValue(a, out var mapped)) a = mapped;
        if (!IonToMass.ContainsKey(a))
            throw new ArgumentException($"Adduct '{adduct}' is not supported. Supported: {string.Join(", ", IonToMass.Keys)}");
        return a;
    }

    [GeneratedRegex("([A-Z][a-z]*)([0-9]*)")]
    private static partial Regex FormulaRegex();

    /// <summary>Parses a formula into (element, count) tokens, exactly like re.findall(CHEM_FORMULA_SIZE, f).</summary>
    public static IEnumerable<(string Element, int Count)> ParseFormula(string formula)
    {
        foreach (Match m in FormulaRegex().Matches(formula))
            yield return (m.Groups[1].Value, m.Groups[2].Value.Length == 0 ? 1 : int.Parse(m.Groups[2].Value));
    }

    /// <summary>formula_to_dense: counts over <see cref="ValidElements"/>; unknown elements are ignored.</summary>
    public static int[] FormulaToDense(string formula)
    {
        var v = new int[ValidElements.Length];
        foreach (var (el, ct) in ParseFormula(formula))
            if (ValidIndex.TryGetValue(el, out var i)) v[i] += ct;
        return v;
    }

    /// <summary>Neutral monoisotopic mass of a formula (elements of <see cref="ValidElements"/> only).</summary>
    public static double MonoisotopicMass(string formula)
    {
        double mass = 0;
        foreach (var (el, ct) in ParseFormula(formula))
        {
            if (!ValidIndex.TryGetValue(el, out var i))
                throw new ArgumentException($"Element '{el}' of formula '{formula}' is not supported");
            mass += ValidMonoMasses[i] * ct;
        }
        return mass;
    }

    /// <summary>Theoretical precursor m/z of a neutral formula with the given adduct.</summary>
    public static double PrecursorMz(string formula, string adduct) =>
        MonoisotopicMass(formula) + IonToMass[StandardizeAdduct(adduct)];

    /// <summary>vec_to_formula: element order of <see cref="ValidElements"/>, count omitted when 1.</summary>
    public static string VecToFormula(ReadOnlySpan<int> vec)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < vec.Length; i++)
        {
            if (vec[i] <= 0) continue;
            sb.Append(ValidElements[i]);
            if (vec[i] > 1) sb.Append(vec[i]);
        }
        return sb.ToString();
    }
}
