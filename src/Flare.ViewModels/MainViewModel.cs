using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flare.Models.Analysis;
using Flare.Models.Plot;
using Flare.Models.Selection;
using Flare.Models.Spectra;
using Flare.Services.Abstraction;

namespace Flare.ViewModels;

/// <summary>
/// Main screen: loads spectrum files and the FLARE model, runs the analysis for the selected spectrum and
/// keeps peak and atom highlights in sync with the shared <see cref="SelectionState"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFlareService _flare;
    private readonly ISpectrumFileService _spectrumFiles;
    private readonly IMoleculeService _molecules;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settings;
    private readonly SelectionState _selection;

    private AnalysisResult? _result;
    private float[] _normalized = [];

    /// <summary>SMILES whose formula/precursor were filled in by the last analysis (cleared when SMILES changes).</summary>
    private string? _derivedFromSmiles;
    private int _analysisVersion;

    public MainViewModel(
        IFlareService flare,
        ISpectrumFileService spectrumFiles,
        IMoleculeService molecules,
        IFilePickerService filePicker,
        IDialogService dialogs,
        ISettingsStore settings,
        SelectionState selection,
        SpectrumViewModel spectrum,
        StructureViewModel structure,
        ManualSpectrumViewModel manualSpectrum,
        CandidatesViewModel candidates)
    {
        _flare = flare;
        _spectrumFiles = spectrumFiles;
        _molecules = molecules;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _settings = settings;
        _selection = selection;
        Spectrum = spectrum;
        Structure = structure;
        ManualSpectrum = manualSpectrum;
        Candidates = candidates;
        Candidates.PropertyChanged += OnCandidatesPropertyChanged;

        Adducts = flare.SupportedAdducts;
        _normalization = ParseEnum(settings.Get(SettingsKeys.Normalization, ""), HighlightNormalization.GlobalMinMax);
        _colormap = ParseEnum(settings.Get(SettingsKeys.Colormap, ""), ColormapType.Viridis);
        _softmaxTemperature = settings.Get(SettingsKeys.SoftmaxTemperature, HighlightCalculator.DefaultTemperature);
        _highlightThreshold = Math.Clamp(settings.Get(SettingsKeys.HighlightThreshold, 0.0), 0, 1);

        _selection.PropertyChanged += OnSelectionChanged;
    }

    public SpectrumViewModel Spectrum { get; }
    public StructureViewModel Structure { get; }
    public ManualSpectrumViewModel ManualSpectrum { get; }
    public CandidatesViewModel Candidates { get; }

    public ObservableCollection<SpectrumRecord> Spectra { get; } = [];
    public IReadOnlyList<string> Adducts { get; }
    public IReadOnlyList<HighlightNormalization> Normalizations { get; } = Enum.GetValues<HighlightNormalization>();
    public IReadOnlyList<ColormapType> Colormaps { get; } = Enum.GetValues<ColormapType>();

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private SpectrumRecord? _selectedSpectrum;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string? _smiles;

    [ObservableProperty]
    private string? _formula;

    [ObservableProperty]
    private string? _adduct;

    [ObservableProperty]
    private string? _precursorMzText;

    [ObservableProperty]
    private HighlightNormalization _normalization;

    [ObservableProperty]
    private ColormapType _colormap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoftmax))]
    private double _softmaxTemperature;

    /// <summary>Normalised weights below this value (0 … 1) are not coloured.</summary>
    [ObservableProperty]
    private double _highlightThreshold;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    private float? _score;

    [ObservableProperty]
    private string _modelPathInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelStatusText))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string? _modelPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(OpenSpectrumFileCommand), nameof(BrowseModelCommand),
        nameof(LoadSelectedModelCommand), nameof(UseDefaultModelCommand),
        nameof(AnalyzeManualSpectrumCommand), nameof(RankCandidatesCommand))]
    [NotifyPropertyChangedFor(nameof(ModelStatusText))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Kein Modell geladen";

    public bool IsSoftmax => Normalization == HighlightNormalization.Softmax;
    public string ScoreText => Score is float s ? s.ToString("0.000", CultureInfo.InvariantCulture) : "–";
    public string ModelName => ModelPath is null ? "kein Modell" : Path.GetFileName(ModelPath);
    public string ModelStatusText => IsBusy && ModelPathInput.Trim() != ModelPath
        ? "Modell wird geladen …"
        : ModelPath is not null && ModelPath == ModelPathInput.Trim() && _flare.IsModelLoaded ? "Modell ist geladen und bereit." : "Kein gültiges Modell geladen.";

    /// <summary>Loads the last used or default model. Called once when the main page appears.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_flare.IsModelLoaded) return;
        var stored = _settings.Get(SettingsKeys.ModelPath, "");
        var path = File.Exists(stored) ? stored : _flare.FindDefaultModelPath();
        if (path is null)
        {
            StatusText = "Kein Modell gefunden – bitte flare.onnx auswählen";
            return;
        }
        await LoadModelAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task OpenSpectrumFileAsync()
    {
        var path = await _filePicker.PickFileAsync("Spektrendatei öffnen", _spectrumFiles.SupportedExtensions);
        if (path is null) return;
        await OpenSpectrumFileAsync(path);
    }

    /// <summary>Loads all spectra of <paramref name="path"/> and selects the first one.</summary>
    public async Task OpenSpectrumFileAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusText = $"Lade {Path.GetFileName(path)} …";
            var spectra = await _spectrumFiles.LoadAsync(path);
            Spectra.Clear();
            foreach (var s in spectra) Spectra.Add(s);
            FileName = Path.GetFileName(path);
            StatusText = $"{spectra.Count} Spektren geladen";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler beim Laden";
            await _dialogs.ShowErrorAsync("Datei konnte nicht geladen werden", ex.Message);
            return;
        }
        finally
        {
            IsBusy = false;
        }
        SelectedSpectrum = Spectra.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task BrowseModelAsync()
    {
        var path = await _filePicker.PickFileAsync("FLARE ONNX-Modell auswählen", [".onnx"]);
        if (path is null) return;
        await LoadModelAsync(path);
        if (_flare.IsModelLoaded && CanAnalyze()) await AnalyzeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task LoadSelectedModelAsync()
    {
        if (string.IsNullOrWhiteSpace(ModelPathInput))
        {
            await _dialogs.ShowErrorAsync("Pfad fehlt", "Bitte den Pfad zur ONNX-Datei eingeben oder eine Datei auswählen.");
            return;
        }
        await LoadModelAsync(ModelPathInput.Trim());
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task UseDefaultModelAsync()
    {
        var path = _flare.FindDefaultModelPath();
        if (path is null)
        {
            await _dialogs.ShowErrorAsync("Kein Standardmodell", "Es wurde kein flare.onnx gefunden. Bitte eine ONNX-Datei auswählen.");
            return;
        }
        await LoadModelAsync(path);
    }

    private async Task LoadModelAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusText = "Lade Modell …";
            await _flare.LoadModelAsync(path);
            ModelPath = path;
            ModelPathInput = path;
            _settings.Set(SettingsKeys.ModelPath, path);
            StatusText = $"Modell geladen: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ModelPathInput = ModelPath ?? "";
            StatusText = "Modell konnte nicht geladen werden";
            var message = ex is FileNotFoundException ? $"ONNX-Modell nicht gefunden: {path}" : ex.Message;
            await _dialogs.ShowErrorAsync("Modell konnte nicht geladen werden", message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ModelName));
            OnPropertyChanged(nameof(ModelStatusText));
        }
    }

    partial void OnModelPathInputChanged(string value) => OnPropertyChanged(nameof(ModelStatusText));

    // Concurrent runs are allowed: a newer request supersedes older ones via _analysisVersion.
    [RelayCommand(CanExecute = nameof(CanAnalyze), AllowConcurrentExecutions = true)]
    private async Task AnalyzeAsync()
    {
        if (SelectedSpectrum is not { } spectrum || string.IsNullOrWhiteSpace(Smiles)) return;
        int version = ++_analysisVersion;
        string smiles = Smiles.Trim();

        if (!TryParsePrecursor(out var precursor))
        {
            await _dialogs.ShowErrorAsync("Ungültige Eingabe", $"Precursor m/z '{PrecursorMzText}' ist keine Zahl.");
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Analysiere …";
            var request = new AnalysisRequest(spectrum, smiles, Formula, Adduct, precursor);
            var result = await _flare.AnalyzeAsync(request);
            var layout = await Task.Run(() => _molecules.CreateLayout(smiles));
            if (version != _analysisVersion) return;

            _selection.Clear();
            _result = result;
            Score = result.Score;
            _derivedFromSmiles = smiles;
            Formula = result.Formula;
            Adduct = result.Adduct;
            PrecursorMzText = result.PrecursorMz.ToString("0.0000", CultureInfo.InvariantCulture);
            Spectrum.SetResult(result);
            Structure.SetMolecule(layout, smiles);
            RecomputeNormalization();
            StatusText = $"Score {ScoreText} · {result.NumPeaks} Peaks · {result.NumAtoms} Atome";
        }
        catch (Exception ex)
        {
            if (version != _analysisVersion) return;
            ClearResult();
            var message = DescribeAnalysisError(ex);
            Structure.SetMolecule(null, smiles, message);
            StatusText = "Analyse fehlgeschlagen";
            await _dialogs.ShowErrorAsync("Analyse fehlgeschlagen", message);
        }
        finally
        {
            if (version == _analysisVersion) IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSelection() => _selection.Clear();

    /// <summary>
    /// Analyses the manually entered peaks with the current candidate (SMILES, formula, adduct, precursor).
    /// The manual spectrum replaces a previous one in the spectrum list and is selected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task AnalyzeManualSpectrumAsync()
    {
        if (!ManualSpectrum.TryGetPeaks(out var peaks))
        {
            await _dialogs.ShowErrorAsync("Ungültiges Spektrum", ManualSpectrum.ErrorMessage ?? "Peaks konnten nicht gelesen werden.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Smiles))
        {
            await _dialogs.ShowErrorAsync("Kein SMILES", "Bitte zuerst ein SMILES für den Kandidaten eingeben.");
            return;
        }
        if (!TryParsePrecursor(out var precursor))
        {
            await _dialogs.ShowErrorAsync("Ungültige Eingabe", $"Precursor m/z '{PrecursorMzText}' ist keine Zahl.");
            return;
        }

        var existing = Spectra.FirstOrDefault(s => s.Title == ManualSpectrumViewModel.Title);
        int index = existing?.Index ?? (Spectra.Count == 0 ? 0 : Spectra.Max(s => s.Index) + 1);
        var record = ManualSpectrumViewModel.ToSpectrum(peaks, index, Smiles.Trim(), NullIfBlank(Formula), NullIfBlank(Adduct), precursor);
        if (existing != null) Spectra[Spectra.IndexOf(existing)] = record;
        else Spectra.Insert(0, record);
        FileName ??= ManualSpectrumViewModel.Title;
        SelectedSpectrum = record;
        if (!_flare.IsModelLoaded)
            await _dialogs.ShowErrorAsync("Kein Modell", "Bitte zuerst das FLARE-Modell (flare.onnx) auswählen.");
    }

    /// <summary>Ranks all candidates against the selected spectrum (FLARE score, highest first).</summary>
    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task RankCandidatesAsync()
    {
        var candidates = Candidates.GetCandidates();
        string? error = SelectedSpectrum is null ? "Bitte zuerst ein Spektrum auswählen."
            : candidates.Count == 0 ? "Bitte Kandidaten-SMILES eingeben (eine pro Zeile oder mit ; getrennt)."
            : !_flare.IsModelLoaded ? "Bitte zuerst das FLARE-Modell (flare.onnx) auswählen."
            : !TryParsePrecursor(out _) ? $"Precursor m/z '{PrecursorMzText}' ist keine Zahl."
            : null;
        if (error != null)
        {
            await _dialogs.ShowErrorAsync("Ranking nicht möglich", error);
            return;
        }

        var spectrum = SelectedSpectrum!;
        TryParsePrecursor(out var precursor);
        // the precursor formula belongs to the spectrum: prefer the MGF value, then the input field,
        // then the formula of the current SMILES (manual spectra)
        string? formula = NullIfBlank(spectrum.Formula) ?? NullIfBlank(Formula)
            ?? (string.IsNullOrWhiteSpace(Smiles) ? null : TryCalculateFormula(Smiles));
        try
        {
            IsBusy = true;
            StatusText = $"Bewerte {candidates.Count} Kandidaten …";
            var scores = await _flare.RankAsync(new RankingRequest(spectrum, candidates, formula, NullIfBlank(Adduct),
                precursor ?? spectrum.PrecursorMz));
            Candidates.SetResults(scores, spectrum.Smiles);
            StatusText = Candidates.Summary ?? "Ranking fertig";
        }
        catch (Exception ex)
        {
            StatusText = "Ranking fehlgeschlagen";
            await _dialogs.ShowErrorAsync("Ranking fehlgeschlagen", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Uses the distinct annotated SMILES of all loaded spectra as candidate list.</summary>
    [RelayCommand]
    private void UseSpectraSmilesAsCandidates() =>
        Candidates.SetCandidates(Spectra.Select(s => s.Smiles).Where(s => !string.IsNullOrWhiteSpace(s))!);

    private string? TryCalculateFormula(string smiles)
    {
        try
        {
            return _molecules.CalculateFormula(smiles.Trim());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnCandidatesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // selecting a ranked candidate shows its peak/atom explanation
        if (e.PropertyName != nameof(CandidatesViewModel.SelectedResult)) return;
        if (Candidates.SelectedResult is not { Candidate.IsValid: true } row || row.Smiles == Smiles?.Trim()) return;
        Smiles = row.Smiles;
        if (CanAnalyze()) AnalyzeCommand.Execute(null);
    }

    private bool TryParsePrecursor(out double? precursor)
    {
        precursor = null;
        if (string.IsNullOrWhiteSpace(PrecursorMzText)) return true;
        if (!double.TryParse(PrecursorMzText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var mz))
            return false;
        precursor = mz;
        return true;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private bool CanRunCommand() => !IsBusy;

    private bool CanAnalyze() =>
        !IsBusy && ModelPath != null && SelectedSpectrum != null && !string.IsNullOrWhiteSpace(Smiles);

    partial void OnSmilesChanged(string? value)
    {
        // formula and precursor computed for a previous candidate must not be reused for a new one
        if (_derivedFromSmiles is null || value?.Trim() == _derivedFromSmiles) return;
        _derivedFromSmiles = null;
        Formula = SelectedSpectrum?.Formula;
        PrecursorMzText = SelectedSpectrum?.PrecursorMz?.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    partial void OnSelectedSpectrumChanged(SpectrumRecord? value)
    {
        _analysisVersion++;
        ClearResult();
        Candidates.Clear();
        Spectrum.SetSpectrum(value);
        Structure.SetMolecule(null, value?.Smiles);
        _derivedFromSmiles = null;
        Smiles = value?.Smiles;
        Formula = value?.Formula;
        Adduct = value?.Adduct;
        PrecursorMzText = value?.PrecursorMz?.ToString("0.0000", CultureInfo.InvariantCulture);
        IsBusy = false;
        if (CanAnalyze()) AnalyzeCommand.Execute(null);
    }

    partial void OnNormalizationChanged(HighlightNormalization value)
    {
        _settings.Set(SettingsKeys.Normalization, value.ToString());
        OnPropertyChanged(nameof(IsSoftmax));
        RecomputeNormalization();
    }

    partial void OnHighlightThresholdChanged(double value)
    {
        _settings.Set(SettingsKeys.HighlightThreshold, value);
        UpdateHighlights();
    }

    partial void OnSoftmaxTemperatureChanged(double value)
    {
        _settings.Set(SettingsKeys.SoftmaxTemperature, value);
        if (Normalization == HighlightNormalization.Softmax) RecomputeNormalization();
    }

    partial void OnColormapChanged(ColormapType value)
    {
        _settings.Set(SettingsKeys.Colormap, value.ToString());
        UpdateHighlights();
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e) => UpdateHighlights();

    private void ClearResult()
    {
        _result = null;
        _normalized = [];
        Score = null;
        _selection.Clear();
        Spectrum.SetResult(null);
        Structure.ApplyWeights(null, Colormap);
    }

    private void RecomputeNormalization()
    {
        _normalized = _result is null ? [] : HighlightCalculator.Normalize(_result, Normalization, SoftmaxTemperature);
        UpdateHighlights();
    }

    /// <summary>
    /// Atoms are coloured for the hovered/selected peak (or the max over all peaks);
    /// peaks are coloured for the hovered/selected atom (or the max over all atoms).
    /// </summary>
    private void UpdateHighlights()
    {
        if (_result is null || _normalized.Length == 0) return;
        int p = _result.NumPeaks, n = _result.NumAtoms;
        int? peak = Valid(_selection.HoveredPeak ?? _selection.SelectedPeak, p);
        int? atom = Valid(_selection.HoveredAtom ?? _selection.SelectedAtom, n);
        float threshold = (float)HighlightThreshold;
        Structure.ApplyWeights(HighlightCalculator.AtomWeights(_normalized, p, n, peak), Colormap, threshold);
        Spectrum.ApplyWeights(HighlightCalculator.PeakWeights(_normalized, p, n, atom), Colormap, threshold);
    }

    private static int? Valid(int? index, int count) => index is int i && i >= 0 && i < count ? i : null;

    private static string DescribeAnalysisError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is DllNotFoundException or FileNotFoundException or BadImageFormatException
                && e.Message.Contains("RDKit", StringComparison.OrdinalIgnoreCase))
            {
                return "Die FLARE-Analyse benötigt RDKit, das nur für Windows (x64) verfügbar ist. "
                     + "Auf dieser Plattform lassen sich Moleküle darstellen, aber nicht aus MGF-Daten vorhersagen.";
            }
        }

        return ex.Message;
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var v) ? v : fallback;
}




