using Flare.Chem;
using Flare.Maui.Services;
using Flare.Maui.Views;
using Flare.Maui.Views.Localization;
using Flare.Models.Selection;
using Flare.Services;
using Flare.Services.Abstraction;
using Flare.ViewModels;
using Flare.ViewModels.Localization;
using Microsoft.Extensions.Logging;
using MolDraw.Maui;
using MolDraw.Core;
#if WINDOWS && FLARE_RDKIT
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

        // RDKit ships Windows x64 natives only, so the chemistry backend is chosen at build time:
        // win-x64 uses RDKit, everything else (Android, win-arm64) uses the NCDK port.
        // Both featurizers emit bit-identical dgllife features (see NcdkDifferentialTests).
#if WINDOWS && FLARE_RDKIT
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
        builder.Services.AddSingleton<ILocalizationService>(serviceProvider =>
        {
            var localization = new LocalizationService();
            var settings = serviceProvider.GetRequiredService<ISettingsStore>();
            localization.SetLanguage(settings.Get(SettingsKeys.Language, localization.Language));
            LocalizationProvider.Current = localization;
            return localization;
        });

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




