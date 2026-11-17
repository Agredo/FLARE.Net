using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flare.Models.Molecules;
using Flare.Models.Plot;
using Flare.Models.Selection;

namespace Flare.ViewModels;

/// <summary>
/// Molecule side of the analysis: atoms with highlight weights and the atom selection.
/// <see cref="AtomWeights"/>, <see cref="SelectedAtomIndex"/> and <see cref="HoveredAtomIndex"/>
/// are shaped for binding to a molecule view (MolDraw.Net <c>MoleculeView</c>).
/// </summary>
public sealed partial class StructureViewModel : ObservableObject
{
    private readonly SelectionState _selection;
    private bool _syncing;

    public StructureViewModel(SelectionState selection)
    {
        _selection = selection;
        _selection.PropertyChanged += OnSelectionChanged;
    }

    public ObservableCollection<AtomRowViewModel> Atoms { get; } = [];

    [ObservableProperty]
    private string? _smiles;

    [ObservableProperty]
    private string? _formula;

    [ObservableProperty]
    private MoleculeLayout? _layout;

    [ObservableProperty]
    private IReadOnlyList<float>? _atomWeights;

    [ObservableProperty]
    private ColormapType _colormap = ColormapType.Viridis;

    [ObservableProperty]
    private AtomRowViewModel? _selectedAtomRow;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Depiction error reported by the molecule view (e.g. SMILES the renderer cannot parse).</summary>
    [ObservableProperty]
    private string? _renderError;

    public bool HasMolecule => Atoms.Count > 0;

    [RelayCommand]
    private void SelectAtom(AtomRowViewModel? atom) => _selection.SelectedAtom = atom?.Index;

    /// <summary>Selected atom index (two-way bindable).</summary>
    public int? SelectedAtomIndex
    {
        get => _selection.SelectedAtom;
        set => _selection.SelectedAtom = value;
    }

    /// <summary>Atom under the pointer (one-way-to-source bindable).</summary>
    public int? HoveredAtomIndex
    {
        get => _selection.HoveredAtom;
        set => _selection.HoveredAtom = value;
    }

    public void SetMolecule(MoleculeLayout? layout, string? smiles, string? error = null)
    {
        Atoms.Clear();
        Layout = layout;
        Smiles = layout?.Smiles ?? smiles;
        Formula = layout?.Formula;
        ErrorMessage = error;
        AtomWeights = null;
        if (layout != null)
            foreach (var a in layout.Atoms) Atoms.Add(new AtomRowViewModel(a.Index, a.Symbol));
        SyncSelectionFlags();
        OnPropertyChanged(nameof(HasMolecule));
    }

    /// <summary>
    /// Applies per-atom weights. Weights below <paramref name="threshold"/> are passed to the view as
    /// <see cref="float.NaN"/> ("no highlight").
    /// </summary>
    public void ApplyWeights(float[]? weights, ColormapType colormap, float threshold = 0f)
    {
        Colormap = colormap;
        AtomWeights = weights?.Select(w => w >= threshold ? w : float.NaN).ToArray();
        for (int i = 0; i < Atoms.Count; i++)
            Atoms[i].ApplyWeight(weights is not null && i < weights.Length ? weights[i] : 0f, colormap, threshold);
    }

    partial void OnSelectedAtomRowChanged(AtomRowViewModel? value)
    {
        if (!_syncing) _selection.SelectedAtom = value?.Index;
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectionState.SelectedAtom):
                SyncSelectionFlags();
                OnPropertyChanged(nameof(SelectedAtomIndex));
                break;
            case nameof(SelectionState.HoveredAtom):
                OnPropertyChanged(nameof(HoveredAtomIndex));
                break;
        }
    }

    private void SyncSelectionFlags()
    {
        _syncing = true;
        try
        {
            AtomRowViewModel? selected = null;
            foreach (var a in Atoms)
            {
                a.IsSelected = a.Index == _selection.SelectedAtom;
                if (a.IsSelected) selected = a;
            }
            SelectedAtomRow = selected;
        }
        finally
        {
            _syncing = false;
        }
    }
}

