namespace Flare.Chem;

/// <summary>
/// Port of FLARE <c>MolToGraph</c> (dgllife "full" atom featurizer, <c>mol_to_bigraph</c> with self
/// loops) from backend-neutral <see cref="MoleculeData"/>. Bit-compatible with the RDKit version.
/// </summary>
public static class MolGraphBuilder
{
    public const int NumFeatures = 78;

    public const int BondSingle = 1;
    public const int BondDouble = 2;
    public const int BondTriple = 4;
    public const int BondAromatic = 8;

    private static readonly string[] AtomTypes = ChemUtils.FeatureElements;

    private static readonly AtomHybridization[] Hybridizations =
        [AtomHybridization.Sp, AtomHybridization.Sp2, AtomHybridization.Sp3, AtomHybridization.Sp3d, AtomHybridization.Sp3d2];

    private static readonly int[] ChiralTags =
        [SmilesScan.ChiralUnspecified, SmilesScan.ChiralCw, SmilesScan.ChiralCcw, SmilesScan.ChiralOther];

    public static MolGraph Build(MoleculeData molecule)
    {
        int n = molecule.Atoms.Count;
        if (n == 0) throw new ArgumentException($"SMILES '{molecule.Smiles}' has no atoms");

        var x = new float[n * NumFeatures];
        for (int i = 0; i < n; i++)
            WriteAtomFeatures(molecule.Atoms[i], x.AsSpan(i * NumFeatures, NumFeatures));

        // dgllife mol_to_bigraph: for each bond (u,v),(v,u) in bond order, then self loops
        var edges = new List<(int, int)>(molecule.Bonds.Count * 2 + n);
        foreach (var bond in molecule.Bonds)
        {
            edges.Add((bond.Begin, bond.End));
            edges.Add((bond.End, bond.Begin));
        }
        for (int i = 0; i < n; i++) edges.Add((i, i));

        return new MolGraph(molecule.Smiles, n, x, edges);
    }

    private static void WriteAtomFeatures(MoleculeAtomData atom, Span<float> f)
    {
        int o = 0;
        f[o++] = (float)(atom.Mass * 0.01);

        int t = Array.IndexOf(AtomTypes, atom.Symbol);
        f[o + (t < 0 ? AtomTypes.Length : t)] = 1;
        o += AtomTypes.Length + 1;

        if ((atom.BondTypeMask & BondSingle) != 0) f[o + 0] = 1;
        if ((atom.BondTypeMask & BondDouble) != 0) f[o + 1] = 1;
        if ((atom.BondTypeMask & BondTriple) != 0) f[o + 2] = 1;
        if ((atom.BondTypeMask & BondAromatic) != 0) f[o + 3] = 1;
        o += 4;

        o = OneHotRange(f, o, atom.Degree, 0, 10);
        o = OneHotRange(f, o, atom.TotalDegree, 0, 5);
        o = OneHotRange(f, o, atom.ExplicitValence, 1, 6);
        o = OneHotRange(f, o, atom.ImplicitValence, 0, 6);
        o = OneHot(f, o, Array.IndexOf(Hybridizations, atom.Hybridization), Hybridizations.Length);
        o = OneHotRange(f, o, atom.TotalHydrogens, 0, 4);
        o = OneHotRange(f, o, atom.FormalCharge, -2, 2);
        o = OneHotRange(f, o, atom.RadicalElectrons, 0, 4);
        o = OneHot(f, o, atom.IsAromatic ? 1 : 0, 2);
        o = OneHot(f, o, atom.IsInRing ? 1 : 0, 2);
        o = OneHot(f, o, Array.IndexOf(ChiralTags, atom.ChiralTag), ChiralTags.Length);
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
