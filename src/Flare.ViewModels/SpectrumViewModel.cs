using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flare.Models.Analysis;
using Flare.Models.Plot;
using Flare.Models.Selection;
using Flare.Models.Spectra;

namespace Flare.ViewModels;

/// <summary>
/// State of the spectrum plot: raw peaks, model peaks with highlight weights, and the visible m/z range.
/// The view raises <see cref="RenderVersion"/> changes to know when to redraw.
/// </summary>
public sealed partial class SpectrumViewModel : ObservableObject
{
    private const double MarginFraction = 0.05;

    private readonly SelectionState _selection;
    private bool _syncing;

    public SpectrumViewModel(SelectionState selection)
    {
        _selection = selection;
        _selection.PropertyChanged += OnSelectionChanged;
    }

    /// <summary>Raw peaks normalised to the base peak.</summary>
    public IReadOnlyList<RawPeak> RawPeaks { get; private set; } = [];

    public ObservableCollection<PeakRowViewModel> Peaks { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private double? _precursorMz;

    [ObservableProperty]
    private bool _hasHighlights;

    [ObservableProperty]
    private PeakRowViewModel? _selectedPeakRow;

    [ObservableProperty]
    private ViewPort _limits = new(0, 100);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenderVersion))]
    private ViewPort _viewPort = new(0, 100);

    private int _renderVersion;

    /// <summary>Incremented whenever the plot content changes.</summary>
    public int RenderVersion => _renderVersion;

    public int? SelectedPeakIndex => _selection.SelectedPeak;
    public int? HoveredPeakIndex => _selection.HoveredPeak;

    public void SetSpectrum(SpectrumRecord? spectrum)
    {
        Peaks.Clear();
        HasHighlights = false;
        SelectedPeakRow = null;
        Title = spectrum?.Title ?? string.Empty;
        PrecursorMz = spectrum?.PrecursorMz;

        if (spectrum is null || spectrum.PeakCount == 0)
        {
            RawPeaks = [];
            Limits = new ViewPort(0, 100);
        }
        else
        {
            double max = spectrum.Intensities.Max();
            RawPeaks = spectrum.Mzs
                .Select((mz, i) => new RawPeak(mz, max > 0 ? spectrum.Intensities[i] / max : 0))
                .OrderBy(p => p.Mz)
                .ToArray();
            double lo = RawPeaks[0].Mz, hi = Math.Max(RawPeaks[^1].Mz, spectrum.PrecursorMz ?? 0);
            double margin = Math.Max((hi - lo) * MarginFraction, 5);
            Limits = new ViewPort(Math.Max(0, lo - margin), hi + margin, 1.1);
        }
        ViewPort = Limits;
        Invalidate();
    }

    public void SetResult(AnalysisResult? result)
    {
        Peaks.Clear();
        SelectedPeakRow = null;
        if (result != null)
        {
            foreach (var p in result.Peaks) Peaks.Add(new PeakRowViewModel(p));
            PrecursorMz = result.PrecursorMz;
        }
        HasHighlights = Peaks.Count > 0;
        SyncSelectionFlags();
        Invalidate();
    }

    public void ApplyWeights(float[] weights, ColormapType colormap, float threshold = 0f)
    {
        for (int i = 0; i < Peaks.Count && i < weights.Length; i++) Peaks[i].ApplyWeight(weights[i], colormap, threshold);
        Invalidate();
    }

    /// <summary>Nearest model peak to <paramref name="mz"/> within <paramref name="toleranceMz"/>.</summary>
    public PeakRowViewModel? HitTest(double mz, double toleranceMz)
    {
        PeakRowViewModel? best = null;
        double bestDist = toleranceMz;
        foreach (var p in Peaks)
        {
            if (p.IsPrecursor) continue;
            double d = Math.Abs(p.Mz - mz);
            if (d <= bestDist)
            {
                bestDist = d;
                best = p;
            }
        }
        return best;
    }

    /// <summary>Selects the peak under the pointer, or clears the selection when clicking empty space.</summary>
    public void SelectAt(double mz, double toleranceMz)
    {
        var hit = HitTest(mz, toleranceMz);
        _selection.SelectedPeak = hit?.Index == _selection.SelectedPeak ? null : hit?.Index;
    }

    public void HoverAt(double? mz, double toleranceMz) =>
        _selection.HoveredPeak = mz is double x ? HitTest(x, toleranceMz)?.Index : null;

    public void ZoomAt(double centerMz, double factor) => ViewPort = ViewPort.Zoom(centerMz, factor, Limits);

    public void PanBy(double deltaMz) => ViewPort = ViewPort.Pan(deltaMz, Limits);

    [RelayCommand]
    private void ResetZoom() => ViewPort = Limits;

    [RelayCommand]
    private void ZoomToSelection()
    {
        if (SelectedPeakRow is null) return;
        double span = Math.Max(Limits.XSpan * 0.1, 10);
        ViewPort = new ViewPort(SelectedPeakRow.Mz - span / 2, SelectedPeakRow.Mz + span / 2, Limits.YMax).ClampTo(Limits);
    }

    partial void OnSelectedPeakRowChanged(PeakRowViewModel? value)
    {
        if (!_syncing) _selection.SelectedPeak = value?.Index;
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectionState.SelectedPeak) or nameof(SelectionState.HoveredPeak))
        {
            SyncSelectionFlags();
            OnPropertyChanged(e.PropertyName == nameof(SelectionState.SelectedPeak)
                ? nameof(SelectedPeakIndex)
                : nameof(HoveredPeakIndex));
            Invalidate();
        }
    }

    private void SyncSelectionFlags()
    {
        _syncing = true;
        try
        {
            PeakRowViewModel? selected = null;
            foreach (var p in Peaks)
            {
                p.IsSelected = p.Index == _selection.SelectedPeak;
                if (p.IsSelected) selected = p;
            }
            SelectedPeakRow = selected;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Invalidate()
    {
        _renderVersion++;
        OnPropertyChanged(nameof(RenderVersion));
    }
}
