namespace Flare.Chem;

/// <summary>
/// A featurised molecule: the 78-wide atom feature matrix (row-major) and the directed edge list
/// used by the FLARE model. Mirrors the tensor layout produced by dgllife's <c>MolGraphConvFeaturizer</c>.
/// </summary>
public sealed record MolGraph(
    string Smiles,
    int NumAtoms,
    float[] AtomFeatures,
    IReadOnlyList<(int Src, int Dst)> Edges);

/// <summary>Molecule backend: SMILES → model input, and SMILES → molecular formula.</summary>
public interface IMoleculeFeaturizer
{
    MolGraph FromSmiles(string smiles);

    string CalculateFormula(string smiles);
}
