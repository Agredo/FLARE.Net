using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Flare.Models.Spectra;
using Flare.ViewModels.Localization;

namespace Flare.ViewModels;

/// <summary>One manually entered peak.</summary>
public sealed record ManualPeak(double Mz, double Intensity);

/// <summary>
/// Spectrum typed in by the user as two semicolon-separated lists (m/z values and intensities),
/// e.g. expected fragments without a measured spectrum. Decimal commas are accepted.
/// </summary>
public sealed partial class ManualSpectrumViewModel : ObservableObject
{
    public const double DefaultIntensity = 100;

    private string? _errorResourceKey;
    private object[]? _errorValues;

    public void RefreshText()
    {
        if (_errorResourceKey is null) return;
        ErrorMessage = _errorValues is null ? Strings.Get(_errorResourceKey)
            : string.Format(CultureInfo.CurrentCulture, Strings.Get(_errorResourceKey), _errorValues);
        OnPropertyChanged(nameof(ErrorMessage));
    }

    public static string Title => Strings.Get("ManualSpectrum");

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
            _errorResourceKey = null;
            _errorValues = null;
            ErrorMessage = null;
            return true;
        }
        catch (FormatException ex) when (ex.Data["resourceKey"] is string key)
        {
            peaks = [];
            _errorResourceKey = key;
            _errorValues = ex.Data["values"] as object[];
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
        if (mzs.Count == 0) throw Error("NoMzEntered");
        var intensities = SplitNumbers(intensityValues, Strings.Get("Intensity"));
        if (intensities.Count == 0) intensities = Enumerable.Repeat(DefaultIntensity, mzs.Count).ToList();
        if (intensities.Count != mzs.Count)
            throw Error("IntensityCountMismatch", mzs.Count, intensities.Count);

        var peaks = mzs.Zip(intensities, (mz, i) => new ManualPeak(mz, i)).OrderBy(p => p.Mz).ToList();
        if (peaks.FirstOrDefault(p => p.Mz <= 0) is { } badMz) throw Error("MzPositive", badMz.Mz);
        if (peaks.FirstOrDefault(p => p.Intensity <= 0) is { } badI) throw Error("IntensityPositive", badI.Mz);
        return peaks;
    }

    private static FormatException Error(string key, params object[] values)
    {
        var exception = new FormatException(string.Format(CultureInfo.CurrentCulture, Strings.Get(key), values));
        exception.Data["resourceKey"] = key;
        exception.Data["values"] = values;
        return exception;
    }

    private static List<double> SplitNumbers(string? text, string name) =>
        (text ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw Error("NotNumber", name, s))
            .ToList();
}
