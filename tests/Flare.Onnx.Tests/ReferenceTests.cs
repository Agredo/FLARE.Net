using System.Text.Json;
using Flare.Onnx;
using Flare.Rdkit;

namespace Flare.Onnx.Tests;

/// <summary>
/// Compares the C# port with golden data produced by the ORIGINAL Python/DGL pipeline
/// (flare_onnx/make_reference.py) and the exported model (flare_onnx/export_onnx.py).
/// </summary>
public sealed class ReferenceTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly JsonElement Ref = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepoRoot, "tests", "Fixtures", "reference.json"))).RootElement;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "flare_onnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root with flare_onnx/ not found");
    }

    private static string OnnxPath =>
        Environment.GetEnvironmentVariable("FLARE_ONNX") ?? Path.Combine(RepoRoot, "flare_onnx", "flare.onnx");

    private static IEnumerable<JsonElement> Spectra => Ref.GetProperty("spectra").EnumerateArray();

    private static double[] Doubles(JsonElement e) => e.EnumerateArray().Select(x => x.GetDouble()).ToArray();
    private static string[] Strings(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static SpectrumInput Input(JsonElement s)
    {
        var i = s.GetProperty("input");
        return new SpectrumInput(
            Doubles(i.GetProperty("mzs")), Doubles(i.GetProperty("intensities")),
            i.GetProperty("formula").GetString()!, i.GetProperty("adduct").GetString()!,
            i.GetProperty("precursor_mz").GetDouble(), i.GetProperty("parent_mass").GetDouble());
    }

    [Fact]
    public void ProcessedPeaks_MatchPython()
    {
        foreach (var s in Spectra)
        {
            var input = Input(s);
            var peaks = SpectrumPreprocessor.Process(input.Mzs, input.Intensities, input.ParentMass!.Value);
            var expected = s.GetProperty("processed_peaks").EnumerateArray().Select(Doubles)
                .OrderBy(p => p[0]).ToArray();
            var actual = peaks.OrderBy(p => p.Mz).ToArray(); // tie order of numpy's argsort is not portable
            Assert.Equal(expected.Length, actual.Length);
            for (int k = 0; k < actual.Length; k++)
            {
                Assert.Equal(expected[k][0], actual[k].Mz, 12);
                Assert.Equal(expected[k][1], actual[k].Intensity, 12);
            }
        }
    }

    [Fact]
    public void SubformulaAssignmentAndFeatures_MatchPython()
    {
        foreach (var s in Spectra)
        {
            var input = Input(s);
            var processed = SpectrumPreprocessor.Process(input.Mzs, input.Intensities, input.ParentMass!.Value);
            var assigned = new SubformulaAssigner(input.Formula, input.Adduct).Assign(processed, Ref.GetProperty("mass_diff_thresh_ppm").GetDouble());

            var exp = s.GetProperty("assigned");
            var expAssigned = Doubles(exp.GetProperty("mz")).Zip(Strings(exp.GetProperty("formula")))
                .OrderBy(t => t.First).ToArray();
            Assert.Equal(expAssigned, assigned.Select(p => (p.Mz, p.Formula)).OrderBy(t => t.Mz).ToArray());

            var peaks = SpectrumFeaturizer.AddPrecursor(assigned, input.Formula, input.PrecursorMz);
            var expPeaks = s.GetProperty("peaks");
            Assert.Equal(Doubles(expPeaks.GetProperty("mz")), peaks.Select(p => p.Mz).ToArray());
            Assert.Equal(Strings(expPeaks.GetProperty("formula")), peaks.Select(p => p.Formula).ToArray());
            Assert.Equal(Doubles(expPeaks.GetProperty("intensity")), peaks.Select(p => p.Intensity).ToArray());

            var x = SpectrumFeaturizer.Featurize(peaks, out int p);
            var expX = s.GetProperty("spec_features").EnumerateArray().SelectMany(Doubles).ToArray();
            Assert.Equal(expX.Length, x.Length);
            for (int k = 0; k < x.Length; k++) Assert.Equal((float)expX[k], x[k]);
        }
    }

    [Fact]
    public void MoleculeGraphs_MatchDgllife()
    {
        foreach (var m in Ref.GetProperty("molecules").EnumerateArray())
        {
            var g = MolFeaturizer.FromSmiles(m.GetProperty("smiles").GetString()!);
            Assert.Equal(m.GetProperty("num_atoms").GetInt32(), g.NumAtoms);

            var expX = m.GetProperty("atom_features").EnumerateArray().SelectMany(Doubles).ToArray();
            Assert.Equal(expX.Length, g.AtomFeatures.Length);
            for (int k = 0; k < expX.Length; k++)
                Assert.True(Math.Abs(expX[k] - g.AtomFeatures[k]) < 1e-6,
                    $"{g.Smiles}: atom {k / MolFeaturizer.NumFeatures} feature {k % MolFeaturizer.NumFeatures}: expected {expX[k]}, got {g.AtomFeatures[k]}");

            var expEdges = m.GetProperty("edges").EnumerateArray()
                .Select(e => (e[0].GetInt32(), e[1].GetInt32())).ToArray();
            Assert.Equal(expEdges, g.Edges.ToArray());
        }
    }

    [Fact]
    public void EndToEndScores_MatchOriginalModel()
    {
        Assert.True(File.Exists(OnnxPath), $"ONNX model not found at {OnnxPath}; run flare_onnx/export_onnx.py or set FLARE_ONNX");
        using var pipeline = new FlarePipeline(OnnxPath, new RdkitMoleculeFeaturizer());
        var candidates = Strings(Ref.GetProperty("candidates"));
        var graphs = candidates.Select(MolFeaturizer.FromSmiles).ToList();

        double maxDiff = 0;
        foreach (var s in Spectra)
        {
            var spectrum = pipeline.PrepareSpectrum(Input(s));
            var scores = pipeline.Score(spectrum, graphs);
            var expected = Doubles(s.GetProperty("scores"));
            for (int k = 0; k < scores.Length; k++) maxDiff = Math.Max(maxDiff, Math.Abs(expected[k] - scores[k]));

            var ranked = pipeline.Rank(Input(s), candidates);
            var trueSmiles = s.GetProperty("input").GetProperty("true_smiles").GetString();
            Assert.Equal(candidates[Array.IndexOf(expected, expected.Max())], ranked[0].Smiles);
            Assert.Contains(ranked, r => r.Smiles == trueSmiles);
        }
        Assert.True(maxDiff < 1e-4, $"max score difference {maxDiff}");
    }
}



