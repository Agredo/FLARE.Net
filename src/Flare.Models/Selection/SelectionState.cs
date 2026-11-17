using CommunityToolkit.Mvvm.ComponentModel;

namespace Flare.Models.Selection;

/// <summary>
/// Shared peak/atom selection (singleton). Spectrum and structure views both read and write it,
/// so selecting a peak recolours the atoms and selecting an atom recolours the peaks.
/// Indices refer to rows/columns of <see cref="Analysis.AnalysisResult.Similarity"/>.
/// </summary>
public sealed partial class SelectionState : ObservableObject
{
    [ObservableProperty]
    private int? _selectedPeak;

    [ObservableProperty]
    private int? _hoveredPeak;

    [ObservableProperty]
    private int? _selectedAtom;

    [ObservableProperty]
    private int? _hoveredAtom;

    public void Clear()
    {
        SelectedPeak = null;
        HoveredPeak = null;
        SelectedAtom = null;
        HoveredAtom = null;
    }
}
