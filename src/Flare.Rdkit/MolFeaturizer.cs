using Flare.Chem;
using GraphMolWrap;

namespace Flare.Rdkit;

/// <summary>
/// Port of FLARE MolToGraph (dgllife "full" atom featurizer, mol_to_bigraph with self loops,
/// RDKit atom order) using RDKit2DotNetStandard.
/// </summary>
public static class MolFeaturizer
{
    public const int NumFeatures = MolGraphBuilder.NumFeatures;

    private static readonly string[] AtomTypes = ChemUtils.FeatureElements;
    private static readonly Bond.BondType[] BondTypes =
        [Bond.BondType.SINGLE, Bond.BondType.DOUBLE, Bond.BondType.TRIPLE, Bond.BondType.AROMATIC];
    private static readonly Atom.HybridizationType[] Hybridizations =
    [
        Atom.HybridizationType.SP, Atom.HybridizationType.SP2, Atom.HybridizationType.SP3,
        Atom.HybridizationType.SP3D, Atom.HybridizationType.SP3D2,
    ];
    private static readonly Atom.ChiralType[] ChiralTags =
    [
        Atom.ChiralType.CHI_UNSPECIFIED, Atom.ChiralType.CHI_TETRAHEDRAL_CW,
        Atom.ChiralType.CHI_TETRAHEDRAL_CCW, Atom.ChiralType.CHI_OTHER,
    ];

    /// <summary>Neutral molecular formula (Hill notation, charge signs removed) computed by RDKit.</summary>
    public static string CalcFormula(string smiles)
    {
        using var mol = RWMol.MolFromSmiles(smiles)
            ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
        return System.Text.RegularExpressions.Regex.Replace(RDKFuncs.calcMolFormula(mol), @"[+-]\d*$", "");
    }

    public static MolGraph FromSmiles(string smiles)
    {
        using var mol = RWMol.MolFromSmiles(smiles)
            ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
        int n = (int)mol.getNumAtoms();
        if (n == 0) throw new ArgumentException($"SMILES '{smiles}' has no atoms");

        var x = new float[n * NumFeatures];
        for (int i = 0; i < n; i++)
        {
            using var atom = mol.getAtomWithIdx((uint)i);
            WriteAtomFeatures(atom, x.AsSpan(i * NumFeatures, NumFeatures));
        }

        // dgllife mol_to_bigraph: for each bond (u,v),(v,u) in bond order, then self loops
        var edges = new List<(int, int)>();
        int nb = (int)mol.getNumBonds();
        for (int b = 0; b < nb; b++)
        {
            using var bond = mol.getBondWithIdx((uint)b);
            int u = (int)bond.getBeginAtomIdx(), v = (int)bond.getEndAtomIdx();
            edges.Add((u, v));
            edges.Add((v, u));
        }
        for (int i = 0; i < n; i++) edges.Add((i, i));
        return new MolGraph(smiles, n, x, edges);
    }

    private static void WriteAtomFeatures(Atom atom, Span<float> f)
    {
        int o = 0;
        f[o++] = (float)(atom.getMass() * 0.01);

        // atom type one-hot with unknown slot
        int t = Array.IndexOf(AtomTypes, atom.getSymbol());
        f[o + (t < 0 ? AtomTypes.Length : t)] = 1;
        o += AtomTypes.Length + 1;

        // any bond of type (SINGLE, DOUBLE, TRIPLE, AROMATIC)
        using (var bonds = atom.getBonds())
        {
            foreach (var bond in bonds)
            {
                int k = Array.IndexOf(BondTypes, bond.getBondType());
                if (k >= 0) f[o + k] = 1;
                bond.Dispose();
            }
        }
        o += BondTypes.Length;

        o = OneHotRange(f, o, (int)atom.getDegree(), 0, 10);
        o = OneHotRange(f, o, (int)atom.getTotalDegree(), 0, 5);
        o = OneHotRange(f, o, atom.getExplicitValence(), 1, 6);
        o = OneHotRange(f, o, atom.getImplicitValence(), 0, 6);
        o = OneHot(f, o, Array.IndexOf(Hybridizations, atom.getHybridization()), Hybridizations.Length);
        o = OneHotRange(f, o, (int)atom.getTotalNumHs(), 0, 4);
        o = OneHotRange(f, o, atom.getFormalCharge(), -2, 2);
        o = OneHotRange(f, o, (int)atom.getNumRadicalElectrons(), 0, 4);
        o = OneHot(f, o, atom.getIsAromatic() ? 1 : 0, 2);
        o = OneHot(f, o, atom.IsInRing() ? 1 : 0, 2);
        o = OneHot(f, o, Array.IndexOf(ChiralTags, atom.getChiralTag()), ChiralTags.Length);
        if (o != NumFeatures) throw new InvalidOperationException($"feature length {o} != {NumFeatures}");
    }

    private static int OneHotRange(Span<float> f, int o, int value, int min, int max) =>
        OneHot(f, o, value >= min && value <= max ? value - min : -1, max - min + 1);

    private static int OneHot(Span<float> f, int o, int index, int length)
    {
        if (index >= 0) f[o + index] = 1;
        return o + length;
    }
}

