using Flare.Models.Spectra;

namespace Flare.Models.Analysis;

/// <summary>
/// Scores several candidate structures against one spectrum. The spectrum is featurised once with the
/// precursor formula (request → spectrum metadata), so all candidates are compared on the same input.
/// </summary>
public sealed record RankingRequest(
    SpectrumRecord Spectrum,
    IReadOnlyList<string> Candidates,
    string? Formula = null,
    string? Adduct = null,
    double? PrecursorMz = null);

/// <summary>
/// One ranked candidate. <see cref="DeltaMz"/> is the candidate's theoretical precursor m/z minus the
/// spectrum's precursor m/z (large values mean the candidate cannot be the measured compound).
/// Invalid candidates have <see cref="Rank"/> 0, no score and an <see cref="Error"/>.
/// </summary>
public sealed record CandidateScore(
    int Rank,
    string Smiles,
    string? Formula,
    float? Score,
    double? DeltaMz,
    string? Error = null)
{
    public bool IsValid => Error is null;
}
