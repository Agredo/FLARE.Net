using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Flare.Models.Spectra;

namespace Flare.ViewModels;

/// <summary>One manually entered peak.</summary>
public sealed record ManualPeak(double Mz, double Intensity);

/// <summary>
/// Spectrum typed in by the user as two semicolon-separated lists (m/z values and intensities),
/// e.g. expected fragments without a measured spectrum. Decimal commas are accepted.
/// </summary>
public sealed partial class ManualSpectrumViewModel : ObservableObject
{
    public const string Title = "Manuelles Spektrum";
    public const double DefaultIntensity = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPeaks))]
    private string? _mzValues;

    [ObservableProperty]
    private string? _intensityValues;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasPeaks => !string.IsNullOrWhiteSpace(MzValues);

    /// <summary>Parses both lists; on failure <see cref="ErrorMessage"/> is set and false is returned.</summary>
    public bool TryGetPeaks(out IReadOnlyList<ManualPeak> peaks)
    {
        try
        {
            peaks = Parse(MzValues, IntensityValues);
            ErrorMessage = null;
            return true;
        }
        catch (FormatException ex)
        {
            peaks = [];
            ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Creates a spectrum record; metadata comes from the candidate input fields.</summary>
    public static SpectrumRecord ToSpectrum(IReadOnlyList<ManualPeak> peaks, int index, string? smiles, string? formula,
        string? adduct, double? precursorMz) =>
        new(index, Title, peaks.Select(p => p.Mz).ToArray(), peaks.Select(p => p.Intensity).ToArray(),
            new Dictionary<string, string>())
        {
            Smiles = smiles,
            Formula = formula,
            Adduct = adduct,
            PrecursorMz = precursorMz,
            MsLevel = 2,
        };

    /// <summary>
    /// Parses "m1; m2; …" and "i1; i2; …". Empty intensities mean <see cref="DefaultIntensity"/> for every peak;
    /// otherwise both lists must have the same length. Peaks are returned sorted by m/z.
    /// </summary>
    public static IReadOnlyList<ManualPeak> Parse(string? mzValues, string? intensityValues)
    {
        var mzs = SplitNumbers(mzValues, "m/z");
        if (mzs.Count == 0) throw new FormatException("Keine m/z-Werte eingegeben.");
        var intensities = SplitNumbers(intensityValues, "Intensität");
        if (intensities.Count == 0) intensities = Enumerable.Repeat(DefaultIntensity, mzs.Count).ToList();
        if (intensities.Count != mzs.Count)
            throw new FormatException($"{mzs.Count} m/z-Werte, aber {intensities.Count} Intensitäten.");

        var peaks = mzs.Zip(intensities, (mz, i) => new ManualPeak(mz, i)).OrderBy(p => p.Mz).ToList();
        if (peaks.FirstOrDefault(p => p.Mz <= 0) is { } badMz) throw new FormatException($"m/z muss > 0 sein ({badMz.Mz}).");
        if (peaks.FirstOrDefault(p => p.Intensity <= 0) is { } badI)
            throw new FormatException($"Intensität muss > 0 sein (m/z {badI.Mz}).");
        return peaks;
    }

    private static List<double> SplitNumbers(string? text, string name) =>
        (text ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new FormatException($"{name} '{s}' ist keine Zahl."))
            .ToList();
}
