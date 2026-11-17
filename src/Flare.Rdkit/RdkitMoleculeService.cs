using Flare.Models.Molecules;
using Flare.Services.Abstraction;
using GraphMolWrap;

namespace Flare.Rdkit;

/// <summary>Creates 2D depictions with RDKit (CoordGen) in the atom order FLARE uses.</summary>
public sealed class RdkitMoleculeService : IMoleculeService
{
    private static readonly object RdkitLock = new();

    public string CalculateFormula(string smiles) => MolFeaturizer.CalcFormula(smiles);

    public MoleculeLayout CreateLayout(string smiles)
    {
        lock (RdkitLock)
        {
            using var mol = RWMol.MolFromSmiles(smiles)
                ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
            if (mol.getNumAtoms() == 0) throw new ArgumentException($"SMILES '{smiles}' has no atoms");

            RDKFuncs.setPreferCoordGen(true);
            mol.compute2DCoords();

            // kekulised copy for drawing bond orders; atom indices are unchanged
            using var kekule = RWMol.MolFromSmiles(smiles);
            RDKFuncs.Kekulize(kekule, true);

            int n = (int)mol.getNumAtoms();
            var x = new double[n];
            var y = new double[n];
            using (var conformer = mol.getConformer())
            {
                for (int i = 0; i < n; i++)
                {
                    using var p = conformer.getAtomPos((uint)i);
                    x[i] = p.x;
                    y[i] = p.y;
                }
            }

            var atoms = new List<LayoutAtom>(n);
            for (int i = 0; i < n; i++)
            {
                using var atom = mol.getAtomWithIdx((uint)i);
                string symbol = atom.getSymbol();
                int charge = atom.getFormalCharge();
                bool showLabel = symbol != "C" || charge != 0 || atom.getDegree() == 0;
                atoms.Add(new LayoutAtom(i, symbol, x[i], y[i], (int)atom.getTotalNumHs(), charge, showLabel));
            }

            var ringCenters = RingCentersPerBond(mol, x, y);
            var bonds = new List<LayoutBond>((int)kekule.getNumBonds());
            for (int b = 0; b < (int)kekule.getNumBonds(); b++)
            {
                using var bond = kekule.getBondWithIdx((uint)b);
                int order = bond.getBondType() switch
                {
                    Bond.BondType.DOUBLE => 2,
                    Bond.BondType.TRIPLE => 3,
                    _ => 1,
                };
                var center = ringCenters.TryGetValue(b, out var c) ? c : ((double, double)?)null;
                bonds.Add(new LayoutBond((int)bond.getBeginAtomIdx(), (int)bond.getEndAtomIdx(), order,
                    center?.Item1, center?.Item2));
            }

            return new MoleculeLayout(smiles, CalculateFormula(smiles), atoms, bonds);
        }
    }

    /// <summary>Centre of the smallest ring each ring bond belongs to.</summary>
    private static Dictionary<int, (double X, double Y)> RingCentersPerBond(ROMol mol, double[] x, double[] y)
    {
        var result = new Dictionary<int, (double X, double Y)>();
        var sizes = new Dictionary<int, int>();
        using var info = mol.getRingInfo();
        using var atomRings = info.atomRings();
        using var bondRings = info.bondRings();
        for (int r = 0; r < atomRings.Count; r++)
        {
            var ringAtoms = atomRings[r];
            double cx = ringAtoms.Average(a => x[a]), cy = ringAtoms.Average(a => y[a]);
            foreach (int b in bondRings[r])
            {
                if (sizes.TryGetValue(b, out var size) && size <= ringAtoms.Count) continue;
                sizes[b] = ringAtoms.Count;
                result[b] = (cx, cy);
            }
        }
        return result;
    }
}

