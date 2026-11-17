using Flare.Models.Spectra;

namespace Flare.Models.Analysis;

/// <summary>
/// Explains one spectrum with one structure. Null values fall back to the spectrum metadata
/// (formula: computed from the SMILES, adduct: [M+H]+, precursor m/z: computed from formula + adduct).
/// </summary>
public sealed record AnalysisRequest(
    SpectrumRecord Spectrum,
    string Smiles,
    string? Formula = null,
    string? Adduct = null,
    double? PrecursorMz = null);
