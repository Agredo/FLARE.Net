using Flare.Rdkit;
using Flare.Services;
using MolDraw.Core.Ncdk;

namespace Flare.ViewModels.Tests;

/// <summary>FLARE's per-atom weights are indexed in RDKit order; MolDraw must depict atoms in the same order.</summary>
public class AtomOrderTests
{
    public static IEnumerable<object[]> SampleSmiles()
    {
        var path = MainViewModelTests.SampleMgf();
        return new MgfSpectrumFileService().LoadAsync(path).GetAwaiter().GetResult()
            .Where(s => !string.IsNullOrEmpty(s.Smiles))
            .Select(s => s.Smiles!)
            .Distinct()
            .Select(s => new object[] { s });
    }

    [Theory]
    [MemberData(nameof(SampleSmiles))]
    public void MolDrawAtomOrderMatchesRdkit(string smiles)
    {
        var rdkit = new RdkitMoleculeService().CreateLayout(smiles);
        var depiction = new DepictionBuilder().Build(new MoleculeLoader().ParseSmiles(smiles));

        Assert.Equal(rdkit.Atoms.Select(a => a.Symbol), depiction.Atoms.Select(a => a.Symbol));
    }
}

