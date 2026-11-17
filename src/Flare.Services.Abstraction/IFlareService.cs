using Flare.Models.Analysis;

namespace Flare.Services.Abstraction;

/// <summary>Runs the FLARE ONNX model. Implementations must be thread-safe and must not block the caller.</summary>
public interface IFlareService
{
    string? ModelPath { get; }
    bool IsModelLoaded { get; }

    /// <summary>Adducts the subformula assignment supports (canonical names).</summary>
    IReadOnlyList<string> SupportedAdducts { get; }

    /// <summary>Looks for flare.onnx next to the application or in the repository (flare_onnx/flare.onnx).</summary>
    string? FindDefaultModelPath();

    Task LoadModelAsync(string path, CancellationToken cancellationToken = default);

    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>Scores all candidates against the spectrum; valid candidates sorted by decreasing score, invalid ones last.</summary>
    Task<IReadOnlyList<CandidateScore>> RankAsync(RankingRequest request, CancellationToken cancellationToken = default);
}

