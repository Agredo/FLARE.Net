namespace Flare.Chem;

/// <summary>Hybridisation buckets exposed by the model (subset of RDKit's <c>HybridizationType</c>).</summary>
public enum AtomHybridization
{
    Unset = 0,
    S = 1,
    Sp = 2,
    Sp2 = 3,
    Sp3 = 4,
    Sp3d = 5,
    Sp3d2 = 6,
}

/// <summary>Backend-neutral per-atom data required to reproduce RDKit's 78-feature vector.</summary>
/// <param name="Mass">RDKit's <c>atom.getMass()</c>, i.e. the exact isotope mass when one is labelled.</param>
public sealed record MoleculeAtomData(
    string Symbol,
    int AtomicNumber,
    double Mass,
    int Degree,
    int TotalDegree,
    int ExplicitValence,
    int ImplicitValence,
    AtomHybridization Hybridization,
    int TotalHydrogens,
    int FormalCharge,
    int RadicalElectrons,
    bool IsAromatic,
    bool IsInRing,
    int ChiralTag,
    int BondTypeMask);

/// <summary>Backend-neutral bond. <paramref name="Order"/> is 1/2/3, or 0 for an aromatic bond.</summary>
public sealed record MoleculeBondData(int Begin, int End, int Order, bool IsAromatic);

/// <summary>A parsed molecule in the exact atom order produced by the SMILES parser.</summary>
public sealed class MoleculeData(string smiles, IReadOnlyList<MoleculeAtomData> atoms, IReadOnlyList<MoleculeBondData> bonds)
{
    public string Smiles { get; } = smiles;
    public IReadOnlyList<MoleculeAtomData> Atoms { get; } = atoms;
    public IReadOnlyList<MoleculeBondData> Bonds { get; } = bonds;
}
