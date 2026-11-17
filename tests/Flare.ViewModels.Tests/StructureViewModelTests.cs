using Flare.Models.Molecules;
using Flare.Models.Plot;
using Flare.Models.Selection;

namespace Flare.ViewModels.Tests;

public class StructureViewModelTests
{
    [Fact]
    public void AtomCommand_SelectsSameAtomAsStructureAndMatchesHighlightColors()
    {
        var selection = new SelectionState();
        var vm = new StructureViewModel(selection);
        var atoms = new[]
        {
            new LayoutAtom(0, "C", 0, 0, 0, 0, false),
            new LayoutAtom(1, "O", 1.5, 0, 0, 0, true),
        };
        vm.SetMolecule(new MoleculeLayout("CO", "CH4O", atoms, []), "CO");

        vm.SelectAtomCommand.Execute(vm.Atoms[1]);
        Assert.Equal(1, vm.SelectedAtomIndex);
        Assert.Same(vm.Atoms[1], vm.SelectedAtomRow);
        Assert.True(vm.Atoms[1].IsSelected);

        vm.ApplyWeights([0.25f, 0.75f], ColormapType.Viridis, 0.5f);
        Assert.Equal(WeightedRowViewModel.NeutralColor, vm.Atoms[0].Color);
        Assert.Equal(Colormaps.Evaluate(ColormapType.Viridis, 0.75f), vm.Atoms[1].Color);
        Assert.True(float.IsNaN(vm.AtomWeights![0]));
        Assert.Equal(0.75f, vm.AtomWeights[1]);
    }
}
