using Flare.Chem;
using Flare.Models.Selection;
using Flare.Rdkit;
using Flare.Services;
using Flare.Services.Abstraction;

namespace Flare.ViewModels.Tests;

public class ModelSettingsTests
{
    private sealed class NoPicker : IFilePickerService
    {
        public Task<string?> PickFileAsync(string title, IReadOnlyList<string> extensions) =>
            Task.FromResult<string?>(null);
    }

    private sealed class RecordingDialogs : IDialogService
    {
        public List<string> Errors { get; } = [];
        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task MissingModelPath_ShowsErrorAndPreservesLastValidPath()
    {
        using var flare = new FlareService(new NcdkMoleculeFeaturizer());
        var settings = new InMemorySettingsStore();
        var selection = new SelectionState();
        var vm = Create(flare, settings, selection, out var dialogs);
        await vm.InitializeCommand.ExecuteAsync(null);
        string? validPath = vm.ModelPath;
        Assert.NotNull(validPath);

        vm.ModelPathInput = $" {Path.GetFullPath("missing-model.onnx")} ";
        await vm.LoadSelectedModelCommand.ExecuteAsync(null);

        Assert.Equal(validPath, vm.ModelPath);
        Assert.Equal(validPath, vm.ModelPathInput);
        Assert.True(flare.IsModelLoaded);
        Assert.Contains("missing-model.onnx", Assert.Single(dialogs.Errors));
        Assert.Equal("Modell ist geladen und bereit.", vm.ModelStatusText);
    }

    [Fact]
    public async Task EmptyModelPath_ShowsValidationErrorWithoutLoading()
    {
        using var flare = new FlareService(new NcdkMoleculeFeaturizer());
        var vm = Create(flare, new InMemorySettingsStore(), new SelectionState(), out var dialogs);

        await vm.LoadSelectedModelCommand.ExecuteAsync(null);

        Assert.False(flare.IsModelLoaded);
        Assert.Equal("Bitte den Pfad zur ONNX-Datei eingeben oder eine Datei auswählen.", Assert.Single(dialogs.Errors));
    }

    private static MainViewModel Create(FlareService flare, ISettingsStore settings, SelectionState selection,
        out RecordingDialogs dialogs)
    {
        dialogs = new RecordingDialogs();
        return new MainViewModel(flare, new MgfSpectrumFileService(), new RdkitMoleculeService(), new NoPicker(),
            dialogs, settings, selection, new SpectrumViewModel(selection), new StructureViewModel(selection),
            new ManualSpectrumViewModel(), new CandidatesViewModel());
    }
}
