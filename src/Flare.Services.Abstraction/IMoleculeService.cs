using Flare.Models.Molecules;

namespace Flare.Services.Abstraction;

public interface IMoleculeService
{
    /// <summary>2D depiction in FLARE atom order. Throws <see cref="ArgumentException"/> for invalid SMILES.</summary>
    MoleculeLayout CreateLayout(string smiles);

    /// <summary>Neutral molecular formula. Throws <see cref="ArgumentException"/> for invalid SMILES.</summary>
    string CalculateFormula(string smiles);
}
