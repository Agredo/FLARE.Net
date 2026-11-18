using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Flare.Models.Analysis;
using Flare.ViewModels.Localization;

namespace Flare.ViewModels;

/// <summary>Row of the candidate ranking.</summary>
public sealed class CandidateRowViewModel(CandidateScore candidate, int rank, bool isReference) : ObservableObject
{
    public CandidateScore Candidate { get; } = candidate;
    public int Rank { get; } = rank;
    public string Smiles => Candidate.Smiles;

    /// <summary>True when the SMILES equals the spectrum's annotated structure (known answer, e.g. from the MGF).</summary>
    public bool IsReference { get; } = isReference;

    public string RankText => Candidate.IsValid ? Rank.ToString(CultureInfo.InvariantCulture) : "–";
    public string ScoreText => Candidate.Score is float s ? s.ToString("0.000", CultureInfo.InvariantCulture) : Strings.Get("Error");
    public void RefreshText() => OnPropertyChanged(nameof(ScoreText));

    public string DeltaText => Candidate.DeltaMz is double d ? d.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) : "–";
    public string Details => Candidate.Error ?? Candidate.Formula ?? string.Empty;
}

/// <summary>
/// Candidate structures (one SMILES per line or separated by ';') and their FLARE ranking for the selected spectrum.
/// </summary>
public sealed partial class CandidatesViewModel : ObservableObject
{
    /// <summary>Tolerance for <see cref="OnlyMatchingMass"/> in m/z.</summary>
    public const double MassToleranceMz = 0.01;

    private IReadOnlyList<CandidateScore> _scores = [];
    private string? _referenceSmiles;

    public ObservableCollection<CandidateRowViewModel> Results { get; } = [];

    [ObservableProperty]
    private string? _candidatesText;

    [ObservableProperty]
    private bool _onlyMatchingMass;

    [ObservableProperty]
    private CandidateRowViewModel? _selectedResult;

    [ObservableProperty]
    private string? _summary;

    public bool HasResults => Results.Count > 0;

    public IReadOnlyList<string> GetCandidates() =>
        (CandidatesText ?? string.Empty)
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !s.StartsWith('#'))
            .Select(s => s.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct()
            .ToList();

    public void SetCandidates(IEnumerable<string> smiles) => CandidatesText = string.Join(Environment.NewLine, smiles.Distinct());

    /// <summary>Shows a ranking; <paramref name="referenceSmiles"/> marks the known structure of the spectrum.</summary>
    public void SetResults(IReadOnlyList<CandidateScore> scores, string? referenceSmiles)
    {
        _scores = scores;
        _referenceSmiles = string.IsNullOrWhiteSpace(referenceSmiles) ? null : referenceSmiles.Trim();
        Rebuild();
    }

    public void Clear() => SetResults([], null);

    public void RefreshText()
    {
        Rebuild();
        foreach (var row in Results) row.RefreshText();
    }

    partial void OnOnlyMatchingMassChanged(bool value) => Rebuild();

    private void Rebuild()
    {
        int? selectedRank = SelectedResult?.Candidate.IsValid == true ? SelectedResult.Rank : null;
        Results.Clear();
        SelectedResult = null;
        int rank = 0;
        foreach (var c in _scores)
        {
            if (OnlyMatchingMass && c.IsValid && !(c.DeltaMz is double d && Math.Abs(d) <= MassToleranceMz)) continue;
            Results.Add(new CandidateRowViewModel(c, c.IsValid ? ++rank : 0, c.Smiles == _referenceSmiles));
        }
        SelectedResult = Results.FirstOrDefault(r => r.Rank == selectedRank && r.Rank > 0);

        var reference = Results.FirstOrDefault(r => r.IsReference);
        Summary = Results.Count == 0
            ? null
            : reference is null
                ? string.Format(CultureInfo.CurrentCulture, Strings.Get("CandidatesScored"), rank)
                : string.Format(CultureInfo.CurrentCulture, Strings.Get("CandidatesReference"), rank, reference.Rank);
        OnPropertyChanged(nameof(HasResults));
    }
}
