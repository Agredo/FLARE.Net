using System.Globalization;

namespace Flare.Onnx;

/// <summary>One spectrum of an MGF file. Metadata keys are case-insensitive.</summary>
public sealed class MgfSpectrum
{
    public required int Index { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
    public required double[] Mzs { get; init; }
    public required double[] Intensities { get; init; }

    public string? Get(params string[] keys)
    {
        foreach (var k in keys)
            if (Metadata.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
        return null;
    }

    public string Title => Get("TITLE", "NAME", "IDENTIFIER", "SPECTRUMID", "FEATURE_ID") ?? $"Spectrum {Index + 1}";

    /// <summary>PEPMASS (first token) or PRECURSOR_MZ.</summary>
    public double? PrecursorMz => ParseFirst(Get("PEPMASS", "PRECURSOR_MZ", "PRECURSORMZ"));

    public double? ParentMass => ParseFirst(Get("PARENT_MASS", "PARENTMASS"));

    public string? Smiles => Get("SMILES");

    public string? Formula => Get("FORMULA", "MOLECULAR_FORMULA", "PRECURSOR_FORMULA");

    /// <summary>Adduct from ADDUCT/PRECURSOR_TYPE/ION fields, otherwise a guess from CHARGE / IONMODE.</summary>
    public string? Adduct
    {
        get
        {
            var a = Get("ADDUCT", "PRECURSOR_TYPE", "PRECURSORTYPE", "ION_TYPE", "IONTYPE");
            if (a != null) return a;
            var charge = Get("CHARGE") ?? "";
            var mode = Get("IONMODE", "ION_MODE", "POLARITY") ?? "";
            if (charge.Contains('-') || mode.StartsWith("neg", StringComparison.OrdinalIgnoreCase)) return "[M-H]-";
            if (charge.Length > 0 || mode.Length > 0) return "[M+H]+";
            return null;
        }
    }

    public int? MsLevel => int.TryParse(Get("MSLEVEL", "MS_LEVEL"), out var l) ? l : null;

    private static double? ParseFirst(string? s)
    {
        if (s == null) return null;
        var tok = s.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

/// <summary>Minimal reader for Mascot Generic Format (BEGIN IONS / KEY=VALUE / "m/z intensity" / END IONS).</summary>
public static class MgfReader
{
    public static List<MgfSpectrum> Read(string path) => Read(File.ReadLines(path));

    public static List<MgfSpectrum> Read(TextReader reader)
    {
        var lines = new List<string>();
        while (reader.ReadLine() is { } l) lines.Add(l);
        return Read(lines);
    }

    public static List<MgfSpectrum> Read(IEnumerable<string> lines)
    {
        var result = new List<MgfSpectrum>();
        Dictionary<string, string>? meta = null;
        List<double>? mzs = null, ints = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';' or '!') continue;

            if (line.Equals("BEGIN IONS", StringComparison.OrdinalIgnoreCase))
            {
                meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                mzs = [];
                ints = [];
            }
            else if (line.Equals("END IONS", StringComparison.OrdinalIgnoreCase))
            {
                if (meta != null)
                    result.Add(new MgfSpectrum
                    {
                        Index = result.Count,
                        Metadata = meta,
                        Mzs = [.. mzs!],
                        Intensities = [.. ints!],
                    });
                meta = null;
            }
            else if (meta != null)
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && !char.IsDigit(line[0]))
                {
                    meta[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                    continue;
                }
                var parts = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mz)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var inten))
                {
                    mzs!.Add(mz);
                    ints!.Add(inten);
                }
            }
        }
        return result;
    }
}
