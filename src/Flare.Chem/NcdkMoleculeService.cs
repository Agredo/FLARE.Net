using Flare.Models.Molecules;
using Flare.Services.Abstraction;
using NCDK;
using NCDK.Graphs;
using NCDK.Layout;
using NCDK.Smiles;

namespace Flare.Chem;

/// <summary>
/// RDKit-free 2D depiction. Layout comes from NCDK's <see cref="StructureDiagramGenerator"/> and the
/// bond orders stay Kekulé so the renderer can draw inner double-bond lines for rings.
/// </summary>
public sealed class NcdkMoleculeService : IMoleculeService
{
    private readonly NcdkMoleculeFeaturizer _featurizer = new();

    public string CalculateFormula(string smiles) => _featurizer.CalculateFormula(smiles);

    public MoleculeLayout CreateLayout(string smiles)
    {
        var container = new SmilesParser(NCDK.Silent.ChemObjectBuilder.Instance).ParseSmiles(smiles)
            ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
        if (container.Atoms.Count == 0) throw new ArgumentException($"SMILES '{smiles}' has no atoms");

        var generator = new StructureDiagramGenerator();
        generator.GenerateCoordinates(container);

        int n = container.Atoms.Count;
        var atoms = new List<LayoutAtom>(n);
        for (int i = 0; i < n; i++)
        {
            var atom = container.Atoms[i];
            var p = atom.Point2D ?? new NCDK.Numerics.Vector2();
            int charge = atom.FormalCharge ?? 0;
            bool showLabel = atom.Symbol != "C" || charge != 0 || atom.Bonds.Count == 0;
            atoms.Add(new LayoutAtom(i, atom.Symbol, p.X, p.Y, atom.ImplicitHydrogenCount ?? 0, charge, showLabel));
        }

        var atomIndex = new Dictionary<IAtom, int>(n);
        for (int i = 0; i < n; i++) atomIndex[container.Atoms[i]] = i;

        var ringCenters = RingCentersPerBond(container);
        var bonds = new List<LayoutBond>(container.Bonds.Count);
        foreach (var bond in container.Bonds)
        {
            int begin = atomIndex[bond.Begin], end = atomIndex[bond.End];
            int order = bond.Order == BondOrder.Double ? 2 : bond.Order == BondOrder.Triple ? 3 : 1;
            var key = begin < end ? (begin, end) : (end, begin);
            var center = ringCenters.TryGetValue(key, out var c) ? c : ((double, double)?)null;
            bonds.Add(new LayoutBond(begin, end, order, center?.Item1, center?.Item2));
        }

        return new MoleculeLayout(smiles, CalculateFormula(smiles), atoms, bonds);
    }

    /// <summary>Centre of the smallest ring each ring bond belongs to.</summary>
    private static Dictionary<(int, int), (double X, double Y)> RingCentersPerBond(IAtomContainer container)
    {
        var result = new Dictionary<(int, int), (double X, double Y)>();
        var sizes = new Dictionary<(int, int), int>();

        foreach (var path in Cycles.FindRelevant(container).GetPaths())
        {
            double cx = 0, cy = 0;
            foreach (int a in path)
            {
                var p = container.Atoms[a].Point2D ?? new NCDK.Numerics.Vector2();
                cx += p.X;
                cy += p.Y;
            }
            cx /= path.Length;
            cy /= path.Length;

            for (int i = 0; i < path.Length; i++)
            {
                int a = path[i], b = path[(i + 1) % path.Length];
                var key = a < b ? (a, b) : (b, a);
                if (sizes.TryGetValue(key, out int size) && size <= path.Length) continue;
                sizes[key] = path.Length;
                result[key] = (cx, cy);
            }
        }

        return result;
    }
}
