using Flare.Chem;
using Flare.Models.Analysis;
using Flare.Onnx;
using Flare.Services.Abstraction;

namespace Flare.Services;

/// <summary>Thread-safe wrapper around <see cref="FlarePipeline"/>; inference runs on the thread pool.</summary>
public sealed class FlareService : IFlareService, IDisposable
{
    public const string ModelFileName = "flare.onnx";

    /// <summary>Tolerance for mapping a model peak back to the raw spectrum (peaks are merged at 4 decimals).</summary>
    private const double RawPeakToleranceMz = 0.01;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IMoleculeFeaturizer _featurizer;
    private FlarePipeline? _pipeline;

    /// <param name="featurizer">
    /// Molecule featurizer for SMILES input. RDKit and the NCDK port produce identical atom features.
    /// </param>
    public FlareService(IMoleculeFeaturizer featurizer) => _featurizer = featurizer;

    public string? ModelPath { get; private set; }
    public bool IsModelLoaded => _pipeline != null;
    public IReadOnlyList<string> SupportedAdducts { get; } = ChemUtils.IonToMass.Keys.ToList();

    public string? FindDefaultModelPath()
    {
        var env = Environment.GetEnvironmentVariable("FLARE_ONNX");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        var local = Path.Combine(AppContext.BaseDirectory, ModelFileName);
        if (File.Exists(local)) return local;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "flare_onnx", ModelFileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public async Task LoadModelAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("ONNX model not found", path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pipeline = await Task.Run(() => new FlarePipeline(path, _featurizer), cancellationToken).ConfigureAwait(false);
            _pipeline?.Dispose();
            _pipeline = pipeline;
            ModelPath = path;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pipeline = _pipeline ?? throw new InvalidOperationException("No FLARE model loaded");
            return await Task.Run(() => Analyze(pipeline, request), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private AnalysisResult Analyze(FlarePipeline pipeline, AnalysisRequest request)
    {
        var spectrum = request.Spectrum;
        string smiles = request.Smiles.Trim();
        if (smiles.Length == 0) throw new ArgumentException("SMILES is empty");

        string formula = NullIfBlank(request.Formula) ?? NullIfBlank(spectrum.Formula) ?? _featurizer.CalculateFormula(smiles);
        string adduct = ChemUtils.StandardizeAdduct(NullIfBlank(request.Adduct) ?? NullIfBlank(spectrum.Adduct) ?? "[M+H]+");
        double precursorMz = request.PrecursorMz ?? spectrum.PrecursorMz ?? ChemUtils.PrecursorMz(formula, adduct);
        double parentMass = spectrum.ParentMass ?? ChemUtils.MonoisotopicMass(formula);

        var input = new SpectrumInput(spectrum.Mzs, spectrum.Intensities, formula, adduct, precursorMz, parentMass);
        var explanation = pipeline.Explain(input, smiles);

        double baseIntensity = spectrum.Intensities.Length == 0 ? 1 : spectrum.Intensities.Max();
        var peaks = explanation.Spectrum.ModelPeaks
            .Select((p, i) => new ExplainedPeak(
                i,
                p.Mz,
                p.Intensity,
                RelativeIntensity(spectrum.Mzs, spectrum.Intensities, p.Mz, baseIntensity),
                p.Formula,
                p.Formula == formula && Math.Abs(p.Intensity - ChemUtils.PrecursorIntensity) < 1e-9))
            .ToList();

        return new AnalysisResult(smiles, formula, adduct, precursorMz, parentMass, explanation.Score, peaks,
            explanation.Molecule.NumAtoms, explanation.Similarity);
    }

    private static double? RelativeIntensity(double[] mzs, double[] intensities, double mz, double baseIntensity)
    {
        double best = -1;
        for (int i = 0; i < mzs.Length; i++)
            if (Math.Abs(mzs[i] - mz) <= RawPeakToleranceMz) best = Math.Max(best, intensities[i]);
        return best < 0 || baseIntensity <= 0 ? null : best / baseIntensity;
    }

    public async Task<IReadOnlyList<CandidateScore>> RankAsync(RankingRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pipeline = _pipeline ?? throw new InvalidOperationException("No FLARE model loaded");
            return await Task.Run(() => Rank(pipeline, request), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<CandidateScore> Rank(FlarePipeline pipeline, RankingRequest request)
    {
        var spectrum = request.Spectrum;
        string formula = NullIfBlank(request.Formula) ?? NullIfBlank(spectrum.Formula)
            ?? throw new ArgumentException("Für das Ranking wird die Precursor-Formel benötigt (Feld „Formel“ oder FORMULA im MGF).");
        string adduct = ChemUtils.StandardizeAdduct(NullIfBlank(request.Adduct) ?? NullIfBlank(spectrum.Adduct) ?? "[M+H]+");
        double precursorMz = request.PrecursorMz ?? spectrum.PrecursorMz ?? ChemUtils.PrecursorMz(formula, adduct);
        double parentMass = spectrum.ParentMass ?? ChemUtils.MonoisotopicMass(formula);
        var prepared = pipeline.PrepareSpectrum(
            new SpectrumInput(spectrum.Mzs, spectrum.Intensities, formula, adduct, precursorMz, parentMass));

        var valid = new List<(MolGraph Graph, string Smiles, string? Formula, double? Delta)>();
        var invalid = new List<CandidateScore>();
        foreach (var raw in request.Candidates.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct())
        {
            try
            {
                var graph = _featurizer.FromSmiles(raw);
                string? candFormula = null;
                double? delta = null;
                try
                {
                    candFormula = _featurizer.CalculateFormula(raw);
                    delta = ChemUtils.PrecursorMz(candFormula, adduct) - precursorMz;
                }
                catch (Exception e) when (e is ArgumentException or KeyNotFoundException or FormatException)
                {
                    // unsupported element for the mass calculation: the candidate can still be scored
                }
                valid.Add((graph, raw, candFormula, delta));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                invalid.Add(new CandidateScore(0, raw, null, null, null, e.Message));
            }
        }

        var scores = valid.Count == 0 ? [] : pipeline.Score(prepared, valid.Select(v => v.Graph).ToList());
        return valid
            .Select((v, i) => (v, Score: scores[i]))
            .OrderByDescending(t => t.Score)
            .Select((t, r) => new CandidateScore(r + 1, t.v.Smiles, t.v.Formula, t.Score, t.v.Delta))
            .Concat(invalid)
            .ToList();
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public void Dispose()
    {
        _pipeline?.Dispose();
        _gate.Dispose();
    }
}


