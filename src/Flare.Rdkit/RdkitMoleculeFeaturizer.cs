using Flare.Chem;

namespace Flare.Rdkit;

/// <summary>
/// <see cref="IMoleculeFeaturizer"/> backed by RDKit itself. This is the reference implementation the
/// NCDK port is differential-tested against; on Windows it can be used interchangeably with
/// <see cref="NcdkMoleculeFeaturizer"/>.
/// </summary>
public sealed class RdkitMoleculeFeaturizer : IMoleculeFeaturizer
{
    public MolGraph FromSmiles(string smiles) => MolFeaturizer.FromSmiles(smiles);

    public string CalculateFormula(string smiles) => MolFeaturizer.CalcFormula(smiles);
}
