namespace Flare.Models.Molecules;

/// <summary>Atom of a 2D depiction. Coordinates are in RDKit depiction units (bond length ~1.5, y up).</summary>
public sealed record LayoutAtom(
    int Index,
    string Symbol,
    double X,
    double Y,
    int HydrogenCount,
    int Charge,
    bool ShowLabel);

/// <summary>Kekulised bond. Ring bonds carry the centre of their ring to place the inner line of double bonds.</summary>
public sealed record LayoutBond(
    int Begin,
    int End,
    int Order,
    double? RingCenterX = null,
    double? RingCenterY = null);

/// <summary>2D depiction of a molecule; atom indices follow the RDKit / FLARE atom order.</summary>
public sealed record MoleculeLayout(string Smiles, string Formula, IReadOnlyList<LayoutAtom> Atoms, IReadOnlyList<LayoutBond> Bonds)
{
    public double MinX => Atoms.Count == 0 ? 0 : Atoms.Min(a => a.X);
    public double MaxX => Atoms.Count == 0 ? 0 : Atoms.Max(a => a.X);
    public double MinY => Atoms.Count == 0 ? 0 : Atoms.Min(a => a.Y);
    public double MaxY => Atoms.Count == 0 ? 0 : Atoms.Max(a => a.Y);

    /// <summary>Mean bond length, used to scale fonts and highlight radii (1.5 for a single atom).</summary>
    public double BondLength => Bonds.Count == 0
        ? 1.5
        : Bonds.Average(b => Math.Sqrt(
            Math.Pow(Atoms[b.Begin].X - Atoms[b.End].X, 2) + Math.Pow(Atoms[b.Begin].Y - Atoms[b.End].Y, 2)));
}
