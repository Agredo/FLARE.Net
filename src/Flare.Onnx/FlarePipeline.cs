using Flare.Chem;
namespace Flare.Onnx;

/// <summary>Raw MS/MS spectrum plus the precursor information FLARE needs.</summary>
public sealed record SpectrumInput(
    IReadOnlyList<double> Mzs,
    IReadOnlyList<double> Intensities,
    string Formula,
    string Adduct,
    double PrecursorMz,
    double? ParentMass = null);

public sealed record PreparedSpectrum(
    List<Peak> ProcessedPeaks,
    List<FormulaPeak> AssignedPeaks,
    List<FormulaPeak> Peaks,
    float[] Features,
    int NumPeaks)
{
    /// <summary>The peaks fed to the model (m/z &lt;= MaxMz), in the row order of <see cref="Features"/>.</summary>
    public IReadOnlyList<FormulaPeak> ModelPeaks { get; init; } = Peaks;
}

public sealed record RankedCandidate(int Rank, string Smiles, float Score);

/// <summary>
/// Score of one molecule plus its peak-to-atom cosine similarity, row-major
/// [Spectrum.ModelPeaks.Count, Molecule.NumAtoms] (RDKit atom order).
/// </summary>
public sealed record FlareExplanation(PreparedSpectrum Spectrum, MolGraph Molecule, float Score, float[] Similarity)
{
    public float this[int peak, int atom] => Similarity[peak * Molecule.NumAtoms + atom];
}

/// <summary>End-to-end FLARE: preprocessing -> subformula assignment -> featurisation -> ONNX scoring.</summary>
public sealed class FlarePipeline : IDisposable
{
    private readonly FlareModel _model;
    private readonly IMoleculeFeaturizer _featurizer;

    public double MassDiffThresholdPpm { get; init; } = SubformulaAssigner.DefaultMassDiffThresholdPpm;
    public int MaxPeaks { get; init; } = SpectrumPreprocessor.DefaultMaxPeaks;
    public double MaxMz { get; init; } = SpectrumFeaturizer.DefaultMaxMz;

    /// <param name="onnxPath">Path to the exported FLARE model.</param>
    /// <param name="featurizer">
    /// Molecule featurizer producing the dgllife atom features. Any <see cref="IMoleculeFeaturizer"/>
    /// works: RDKit and the NCDK port emit identical feature vectors.
    /// </param>
    public FlarePipeline(string onnxPath, IMoleculeFeaturizer featurizer)
    {
        _model = new FlareModel(onnxPath);
        _featurizer = featurizer;
    }

    public PreparedSpectrum PrepareSpectrum(SpectrumInput input)
    {
        // MIST uses the parent mass only for the "m/z <= parent mass + 1" filter
        double parentMass = input.ParentMass ?? input.PrecursorMz;
        var processed = SpectrumPreprocessor.Process(input.Mzs, input.Intensities, parentMass, maxPeaks: MaxPeaks);
        var assigned = processed.Count == 0
            ? []
            : new SubformulaAssigner(input.Formula, input.Adduct).Assign(processed, MassDiffThresholdPpm);
        if (assigned.Count == 0)
            throw new InvalidOperationException("No peak could be assigned to a subformula of the precursor formula");
        var peaks = SpectrumFeaturizer.AddPrecursor(assigned, input.Formula, input.PrecursorMz);
        var features = SpectrumFeaturizer.Featurize(peaks, out int numPeaks, MaxMz);
        return new PreparedSpectrum(processed, assigned, peaks, features, numPeaks)
        {
            ModelPeaks = peaks.Where(p => p.Mz <= MaxMz).ToList(),
        };
    }

    /// <summary>Scores a single molecule and returns the peak-to-atom similarity used for highlighting.</summary>
    public FlareExplanation Explain(SpectrumInput input, string smiles)
    {
        var spectrum = PrepareSpectrum(input);
        var graph = _featurizer.FromSmiles(smiles);
        var (score, similarity) = _model.Explain(spectrum.Features, spectrum.NumPeaks, graph);
        return new FlareExplanation(spectrum, graph, score, similarity);
    }

    public float[] Score(PreparedSpectrum spectrum, IReadOnlyList<MolGraph> candidates) =>
        _model.Score(spectrum.Features, spectrum.NumPeaks, candidates);

    /// <summary>Scores all candidate SMILES and returns them sorted by decreasing FILIP score.</summary>
    public List<RankedCandidate> Rank(SpectrumInput input, IEnumerable<string> candidateSmiles)
    {
        var spectrum = PrepareSpectrum(input);
        var graphs = candidateSmiles.Select(_featurizer.FromSmiles).ToList();
        var scores = Score(spectrum, graphs);
        return graphs.Select((g, i) => (g.Smiles, Score: scores[i]))
            .OrderByDescending(t => t.Score)
            .Select((t, r) => new RankedCandidate(r + 1, t.Smiles, t.Score))
            .ToList();
    }

    public void Dispose() => _model.Dispose();
}

