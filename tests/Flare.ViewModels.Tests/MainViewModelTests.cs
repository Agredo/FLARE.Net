using Flare.Chem;
using Flare.Models.Analysis;
using Flare.Models.Selection;
using Flare.Rdkit;
using Flare.Services;
using Flare.Services.Abstraction;
using Flare.ViewModels;

namespace Flare.ViewModels.Tests;

public class MainViewModelTests
{
    private sealed class FakePicker(string? path) : IFilePickerService
    {
        public Task<string?> PickFileAsync(string title, IReadOnlyList<string> extensions) => Task.FromResult(path);
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

    internal static string SampleMgf()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "samples", "sample.mgf");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("samples/sample.mgf");
    }

    [Fact]
    public async Task LoadsMgf_AnalyzesFirstSpectrum_AndSyncsHighlights()
    {
        using var flare = new FlareService(new NcdkMoleculeFeaturizer());
        var modelPath = flare.FindDefaultModelPath();
        Assert.NotNull(modelPath);

        var selection = new SelectionState();
        var dialogs = new RecordingDialogs();
        var spectrum = new SpectrumViewModel(selection);
        var structure = new StructureViewModel(selection);
        var vm = new MainViewModel(flare, new MgfSpectrumFileService(), new RdkitMoleculeService(),
            new FakePicker(null), dialogs, new InMemorySettingsStore(), selection, spectrum, structure, new ManualSpectrumViewModel(), new CandidatesViewModel());

        await vm.InitializeCommand.ExecuteAsync(null);
        Assert.True(flare.IsModelLoaded);

        await vm.OpenSpectrumFileAsync(SampleMgf());
        Assert.Equal(25, vm.Spectra.Count);
        Assert.NotNull(vm.SelectedSpectrum);

        // selecting a spectrum with SMILES starts the analysis automatically
        await vm.AnalyzeCommand.ExecuteAsync(null);
        Assert.Empty(dialogs.Errors);
        Assert.NotNull(vm.Score);
        Assert.InRange(vm.Score!.Value, -1f, 1f);
        Assert.NotEmpty(spectrum.Peaks);
        Assert.NotEmpty(structure.Atoms);
        Assert.Equal(structure.Atoms.Count, structure.AtomWeights!.Count);
        Assert.Contains(structure.AtomWeights, w => w > 0.99f); // global max-normalised

        // selecting a peak colours atoms by that peak's row; selecting an atom colours peaks by its column
        var peak = spectrum.Peaks.First(p => !p.IsPrecursor);
        spectrum.SelectedPeakRow = peak;
        Assert.Equal(peak.Index, selection.SelectedPeak);
        Assert.True(peak.IsSelected);

        structure.SelectedAtomIndex = 0;
        Assert.True(structure.Atoms[0].IsSelected);
        Assert.Same(structure.Atoms[0], structure.SelectedAtomRow);

        vm.Normalization = HighlightNormalization.Softmax;
        Assert.All(spectrum.Peaks, p => Assert.InRange(p.Weight, 0f, 1f));

        // weights below the threshold are not coloured (NaN for the molecule view, neutral rows)
        vm.HighlightThreshold = 0.5;
        Assert.All(structure.Atoms, a => Assert.Equal(a.Weight >= 0.5f, a.IsHighlighted));
        Assert.All(structure.AtomWeights!, w => Assert.True(float.IsNaN(w) || w >= 0.5f));
        Assert.All(spectrum.Peaks.Where(p => !p.IsHighlighted),
            p => Assert.Equal(WeightedRowViewModel.NeutralColor, p.Color));
        vm.HighlightThreshold = 0;
        Assert.All(structure.Atoms, a => Assert.True(a.IsHighlighted));

        vm.ClearSelectionCommand.Execute(null);
        Assert.Null(selection.SelectedPeak);
        Assert.Null(selection.SelectedAtom);
    }

    [Fact]
    public async Task RankCandidates_MarksReference_InvalidLast_AndSelectingAnalyzes()
    {
        using var flare = new FlareService(new NcdkMoleculeFeaturizer());
        var selection = new SelectionState();
        var dialogs = new RecordingDialogs();
        var candidates = new CandidatesViewModel();
        var vm = new MainViewModel(flare, new MgfSpectrumFileService(), new RdkitMoleculeService(), new FakePicker(null),
            dialogs, new InMemorySettingsStore(), selection, new SpectrumViewModel(selection), new StructureViewModel(selection),
            new ManualSpectrumViewModel(), candidates);
        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.OpenSpectrumFileAsync(SampleMgf());
        await vm.AnalyzeCommand.ExecuteAsync(null);

        vm.UseSpectraSmilesAsCandidatesCommand.Execute(null);
        candidates.CandidatesText += "\nnot_a_smiles((";
        await vm.RankCandidatesCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        var valid = candidates.Results.Where(r => r.Candidate.IsValid).ToList();
        Assert.Equal(23, valid.Count);
        Assert.Equal(Enumerable.Range(1, 23), valid.Select(r => r.Rank));
        Assert.True(valid.Zip(valid.Skip(1)).All(p => p.First.Candidate.Score >= p.Second.Candidate.Score));
        Assert.False(candidates.Results[^1].Candidate.IsValid);
        var reference = Assert.Single(candidates.Results, r => r.IsReference);
        Assert.Equal(vm.SelectedSpectrum!.Smiles, reference.Smiles);
        Assert.Equal(0, reference.Candidate.DeltaMz!.Value, 2);
        Assert.Contains($"Platz {reference.Rank}", candidates.Summary);

        // the mass filter keeps only candidates matching the precursor m/z
        candidates.OnlyMatchingMass = true;
        Assert.All(candidates.Results.Where(r => r.Candidate.IsValid),
            r => Assert.True(Math.Abs(r.Candidate.DeltaMz!.Value) <= CandidatesViewModel.MassToleranceMz));
        Assert.Contains(candidates.Results, r => r.IsReference);
        candidates.OnlyMatchingMass = false;

        // selecting another candidate loads it as current SMILES
        var other = valid.First(r => !r.IsReference);
        candidates.SelectedResult = other;
        Assert.Equal(other.Smiles, vm.Smiles);
    }
}




