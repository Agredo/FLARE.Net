using System.Globalization;
using System.Text.Json;
using Flare.Chem;
using Flare.Onnx;

const string Usage = """
Usage: flare eval-mgf --model flare.onnx --mgf spectra.mgf
             ranks every annotated spectrum against all distinct SMILES of the file and reports top-k hit rates

       flare --model flare.onnx --spectrum spec.txt --formula C9H10O2 --adduct [M+H]+ --precursor-mz 151.0754
             (--candidates smiles.txt | --smiles SMILES [--smiles SMILES ...])
             [--parent-mass 150.068] [--top 20] [--json]

  --spectrum     text file with one "m/z intensity" pair per line (whitespace, comma or tab separated)
  --candidates   text file with one SMILES per line (first column is used)
""";

try
{
    if (args.Length > 0 && args[0] == "eval-mgf") return EvaluateMgf(ParseArgs(args[1..]));
    var opts = ParseArgs(args);
    string Req(string k) => opts.TryGetValue(k, out var v) && v.Count > 0 ? v[0] : throw new ArgumentException($"--{k} is required");

    var (mzs, ints) = ReadSpectrum(Req("spectrum"));
    var input = new SpectrumInput(
        mzs, ints, Req("formula"), Req("adduct"),
        double.Parse(Req("precursor-mz"), CultureInfo.InvariantCulture),
        opts.TryGetValue("parent-mass", out var pm) ? double.Parse(pm[0], CultureInfo.InvariantCulture) : null);

    var candidates = new List<string>();
    if (opts.TryGetValue("smiles", out var s)) candidates.AddRange(s);
    if (opts.TryGetValue("candidates", out var c))
        candidates.AddRange(File.ReadLines(c[0])
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .Select(l => l.Split(['\t', ',', ' '], StringSplitOptions.RemoveEmptyEntries)[0]));
    if (candidates.Count == 0) throw new ArgumentException("no candidates given");

    using var pipeline = new FlarePipeline(Req("model"), new NcdkMoleculeFeaturizer());
    var ranked = pipeline.Rank(input, candidates.Distinct());
    int top = opts.TryGetValue("top", out var t) ? int.Parse(t[0]) : ranked.Count;

    if (opts.ContainsKey("json"))
        Console.WriteLine(JsonSerializer.Serialize(ranked.Take(top), new JsonSerializerOptions { WriteIndented = true }));
    else
        foreach (var r in ranked.Take(top))
            Console.WriteLine($"{r.Rank,4}  {r.Score.ToString("F5", CultureInfo.InvariantCulture)}  {r.Smiles}");
    return 0;
}
catch (Exception e) when (e is ArgumentException or FileNotFoundException or InvalidOperationException or FormatException)
{
    Console.Error.WriteLine($"error: {e.Message}\n\n{Usage}");
    return 1;
}

static Dictionary<string, List<string>> ParseArgs(string[] args)
{
    var d = new Dictionary<string, List<string>>();
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) throw new ArgumentException($"unexpected argument '{args[i]}'");
        var key = args[i][2..];
        if (!d.TryGetValue(key, out var list)) d[key] = list = [];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) list.Add(args[++i]);
    }
    return d;
}

static (List<double>, List<double>) ReadSpectrum(string path)
{
    var mzs = new List<double>();
    var ints = new List<double>();
    foreach (var line in File.ReadLines(path))
    {
        if (line.TrimStart().StartsWith('#')) continue;
        var parts = line.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) continue;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mz)) continue;
        mzs.Add(mz);
        ints.Add(double.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture));
    }
    return (mzs, ints);
}

// Leave-nothing-out retrieval check: the true structure of each spectrum competes with all other SMILES in the file.
static int EvaluateMgf(Dictionary<string, List<string>> opts)
{
    string Req(string k) => opts.TryGetValue(k, out var v) && v.Count > 0 ? v[0] : throw new ArgumentException($"--{k} is required");
    var spectra = MgfReader.Read(Req("mgf"))
        .Where(s => !string.IsNullOrWhiteSpace(s.Smiles) && !string.IsNullOrWhiteSpace(s.Formula))
        .ToList();
    var candidates = spectra.Select(s => s.Smiles!).Distinct().ToList();
    if (spectra.Count == 0) throw new ArgumentException("no spectra with SMILES and FORMULA found");

    using var pipeline = new FlarePipeline(Req("model"), new NcdkMoleculeFeaturizer());
    var ranks = new List<int>();
    Console.WriteLine($"{spectra.Count} spectra, {candidates.Count} distinct candidate structures\n");
    Console.WriteLine($"{"rank",4}  {"score",7}  {"best",7}  {"formula",-14} title");
    foreach (var s in spectra)
    {
        string adduct = ChemUtils.StandardizeAdduct(s.Adduct ?? "[M+H]+");
        var input = new SpectrumInput(s.Mzs, s.Intensities, s.Formula!, adduct,
            s.PrecursorMz ?? ChemUtils.PrecursorMz(s.Formula!, adduct), s.ParentMass);
        var ranked = pipeline.Rank(input, candidates);
        var hit = ranked.First(r => r.Smiles == s.Smiles);
        ranks.Add(hit.Rank);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{hit.Rank,4}  {hit.Score,7:F4}  {ranked[0].Score,7:F4}  {s.Formula,-14} {s.Title}"));
    }

    double TopK(int k) => ranks.Count(r => r <= k) / (double)ranks.Count;
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"""

        top-1 {TopK(1):P0}   top-3 {TopK(3):P0}   top-5 {TopK(5):P0}   MRR {ranks.Average(r => 1.0 / r):F3}
        mean rank {ranks.Average():F1} (random guessing: {(candidates.Count + 1) / 2.0:F1})
        """));
    return 0;
}

