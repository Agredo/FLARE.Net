using Flare.Chem;
using Flare.Maui.Services;
using Flare.Maui.Views;
using Flare.Models.Selection;
using Flare.Services;
using Flare.Services.Abstraction;
using Flare.ViewModels;
using Microsoft.Extensions.Logging;
using MolDraw.Maui;
using MolDraw.Core;
#if WINDOWS
using Flare.Rdkit;
#endif

namespace Flare.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMolDraw(); // also registers SkiaSharp
        builder.Services.AddMolDrawCore();

        // Generic services (Flare.Services)
        builder.Services.AddSingleton<ISpectrumFileService, MgfSpectrumFileService>();

        // RDKit ships Windows x64 natives only, so the chemistry backend is chosen per platform.
        // Both featurizers emit bit-identical dgllife features (see NcdkDifferentialTests).
#if WINDOWS
        builder.Services.AddSingleton<IMoleculeService, RdkitMoleculeService>();
        builder.Services.AddSingleton<IMoleculeFeaturizer, RdkitMoleculeFeaturizer>();
#else
        builder.Services.AddSingleton<IMoleculeService, NcdkMoleculeService>();
        builder.Services.AddSingleton<IMoleculeFeaturizer, NcdkMoleculeFeaturizer>();
#endif
        builder.Services.AddSingleton<IFlareService, FlareService>();

        // MAUI services (Flare.Maui.Services)
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();

        // Shared selection between spectrum and structure
        builder.Services.AddSingleton<SelectionState>();

        // ViewModels
        builder.Services.AddSingleton<SpectrumViewModel>();
        builder.Services.AddSingleton<StructureViewModel>();
        builder.Services.AddSingleton<ManualSpectrumViewModel>();
        builder.Services.AddSingleton<CandidatesViewModel>();
        builder.Services.AddSingleton<MainViewModel>();

        // Views
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}




