namespace Flare.Models.Spectra;

/// <summary>One MS/MS spectrum as read from a file, with the precursor metadata FLARE can use.</summary>
public sealed record SpectrumRecord(
    int Index,
    string Title,
    double[] Mzs,
    double[] Intensities,
    IReadOnlyDictionary<string, string> Metadata)
{
    public double? PrecursorMz { get; init; }

    /// <summary>Neutral monoisotopic mass of the precursor, if given in the file.</summary>
    public double? ParentMass { get; init; }

    public string? Smiles { get; init; }
    public string? Formula { get; init; }
    public string? Adduct { get; init; }
    public int? MsLevel { get; init; }

    public int PeakCount => Mzs.Length;
}

/// <summary>Raw peak with intensity relative to the base peak (0..1).</summary>
public readonly record struct RawPeak(double Mz, double RelativeIntensity);
