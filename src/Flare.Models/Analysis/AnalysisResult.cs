namespace Flare.Models.Analysis;

/// <summary>A peak as seen by the model, i.e. one row of the similarity matrix.</summary>
/// <param name="Index">Row index in <see cref="AnalysisResult.Similarity"/>.</param>
/// <param name="ModelIntensity">Intensity fed to the model (sqrt-normalised, precursor = 1.1).</param>
/// <param name="RelativeIntensity">Intensity of the matching raw peak relative to the base peak; null if the peak is not in the raw spectrum (added precursor).</param>
public sealed record ExplainedPeak(
    int Index,
    double Mz,
    double ModelIntensity,
    double? RelativeIntensity,
    string Formula,
    bool IsPrecursor);

/// <summary>FLARE score and peak-to-atom cosine similarity of one spectrum/structure pair.</summary>
public sealed record AnalysisResult(
    string Smiles,
    string Formula,
    string Adduct,
    double PrecursorMz,
    double ParentMass,
    float Score,
    IReadOnlyList<ExplainedPeak> Peaks,
    int NumAtoms,
    float[] Similarity)
{
    public int NumPeaks => Peaks.Count;

    /// <summary>Cosine similarity of peak <paramref name="peak"/> and atom <paramref name="atom"/>.</summary>
    public float GetSimilarity(int peak, int atom) => Similarity[peak * NumAtoms + atom];
}
