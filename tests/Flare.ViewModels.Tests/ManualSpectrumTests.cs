using Flare.Chem;
using Flare.Models.Selection;
using Flare.Rdkit;
using Flare.Services;
using Flare.Services.Abstraction;

namespace Flare.ViewModels.Tests;

public class ManualSpectrumTests
{
    private sealed class NoPicker : IFilePickerService
    {
        public Task<string?> PickFileAsync(string title, IReadOnlyList<string> extensions) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingDialogs : IDialogService
    {
        public List<string> Errors { get; } = [];
        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add($"{title}: {message}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Parse_SemicolonLists_WithDecimalCommas_SortedByMz()
    {
        var peaks = ManualSpectrumViewModel.Parse("135,0441; 107.0491;77,0386 ;", "10; 100; 30");
        Assert.Equal([77.0386, 107.0491, 135.0441], peaks.Select(p => p.Mz));
        Assert.Equal([30, 100, 10], peaks.Select(p => p.Intensity));
    }

    [Fact]
    public void Parse_EmptyIntensities_DefaultTo100()
    {
        var peaks = ManualSpectrumViewModel.Parse("107.0491; 77.0386", " ");
        Assert.All(peaks, p => Assert.Equal(ManualSpectrumViewModel.DefaultIntensity, p.Intensity));
    }

    [Theory]
    [InlineData("107.0491; 77.0386", "100")]
    [InlineData("107.0491; abc", "")]
    [InlineData("", "")]
    [InlineData("107.0491", "0")]
    public void TryGetPeaks_InvalidInput_SetsError(string mzs, string intensities)
    {
        var vm = new ManualSpectrumViewModel { MzValues = mzs, IntensityValues = intensities };
        Assert.False(vm.TryGetPeaks(out _));
        Assert.NotNull(vm.ErrorMessage);
    }
    [Fact]
    public async Task AnalyzeManualSpectrum_HydroxyphenylacetamideFragments()
    {
        using var flare = new FlareService(new NcdkMoleculeFeaturizer());
        var selection = new SelectionState();
        var dialogs = new RecordingDialogs();
        var spectrum = new SpectrumViewModel(selection);
        var structure = new StructureViewModel(selection);
        var manual = new ManualSpectrumViewModel();
        var vm = new MainViewModel(flare, new MgfSpectrumFileService(), new RdkitMoleculeService(), new NoPicker(),
            dialogs, new InMemorySettingsStore(), selection, spectrum, structure, manual, new CandidatesViewModel());
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.Smiles = "C1=CC(=CC=C1CC(=O)N)O";
        vm.Adduct = "[M+H]+";
        manual.MzValues = "135,0441; 107,0491; 79,0542; 77,0386";
        manual.IntensityValues = "10; 100; 15; 30";

        await vm.AnalyzeManualSpectrumCommand.ExecuteAsync(null);
        await vm.AnalyzeCommand.ExecuteAsync(null); // the auto-started run is fire-and-forget; await a deterministic one

        Assert.Empty(dialogs.Errors);
        Assert.Equal(ManualSpectrumViewModel.Title, vm.SelectedSpectrum!.Title);
        Assert.Equal("C8H9NO2", vm.Formula);
        Assert.NotNull(vm.Score);
        Assert.Equal(11, structure.Atoms.Count);
        // FLARE annotates neutral subformulas (ion minus the adduct proton): C7H7O+ -> C7H6O
        Assert.Contains(spectrum.Peaks, p => p.Formula == "C7H6O" && Math.Abs(p.Mz - 107.0491) < 0.001);

        // second run replaces the manual entry instead of adding another one
        await vm.AnalyzeManualSpectrumCommand.ExecuteAsync(null);
        Assert.Single(vm.Spectra, s => s.Title == ManualSpectrumViewModel.Title);
    }
}






