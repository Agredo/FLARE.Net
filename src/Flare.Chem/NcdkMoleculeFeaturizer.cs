using NCDK;
using NCDK.Smiles;

namespace Flare.Chem;

/// <summary>RDKit-free molecule backend built on NCDK; drop-in replacement for the RDKit featurizer.</summary>
public sealed class NcdkMoleculeFeaturizer : IMoleculeFeaturizer
{
    public MolGraph FromSmiles(string smiles) => MolGraphBuilder.Build(NcdkMoleculeReader.Read(smiles));

    /// <summary>Neutral molecular formula in Hill notation, matching RDKit's <c>calcMolFormula</c> minus the charge.</summary>
    public string CalculateFormula(string smiles)
    {
        var container = new SmilesParser(NCDK.Silent.ChemObjectBuilder.Instance).ParseSmiles(smiles)
            ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
        if (container.Atoms.Count == 0) throw new ArgumentException($"SMILES '{smiles}' has no atoms");
        return HillFormula(container);
    }

    internal static string HillFormula(IAtomContainer container)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(string symbol, int n) => counts[symbol] = counts.GetValueOrDefault(symbol) + n;

        foreach (var atom in container.Atoms)
        {
            Add(atom.Symbol, 1);
            int h = atom.ImplicitHydrogenCount ?? 0;
            if (h > 0) Add("H", h);
        }

        var ordered = new List<string>();
        if (counts.ContainsKey("C")) ordered.Add("C");
        if (counts.ContainsKey("H")) ordered.Add("H");
        ordered.AddRange(counts.Keys.Where(s => s != "C" && s != "H").OrderBy(s => s, StringComparer.Ordinal));

        var sb = new System.Text.StringBuilder();
        foreach (var symbol in ordered)
        {
            sb.Append(symbol);
            int n = counts[symbol];
            if (n > 1) sb.Append(n);
        }
        return sb.ToString();
    }
}
