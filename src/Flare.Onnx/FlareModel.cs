using Flare.Chem;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Flare.Onnx;

/// <summary>Runs flare.onnx: FILIP similarity between one spectrum and a batch of molecules.</summary>
public sealed class FlareModel : IDisposable
{
    private readonly InferenceSession _session;

    public FlareModel(string onnxPath, SessionOptions? options = null)
    {
        _session = options is null ? new InferenceSession(onnxPath) : new InferenceSession(onnxPath, options);
    }

    /// <summary>Scores one featurised spectrum against all molecules (processed in chunks).</summary>
    public float[] Score(float[] spec, int numPeaks, IReadOnlyList<MolGraph> molecules, int batchSize = 64)
    {
        if (numPeaks == 0) throw new ArgumentException("Spectrum has no peaks");
        if (spec.Length != numPeaks * SpectrumFeaturizer.NumFeatures) throw new ArgumentException("spec has wrong length");

        var scores = new float[molecules.Count];
        var specTensor = new DenseTensor<float>(spec, [numPeaks, SpectrumFeaturizer.NumFeatures]);
        for (int start = 0; start < molecules.Count; start += batchSize)
        {
            var chunk = molecules.Skip(start).Take(batchSize).ToList();
            int b = chunk.Count, n = chunk.Max(m => m.NumAtoms);
            var atoms = new DenseTensor<float>([b, n, MolGraphBuilder.NumFeatures]);
            var adj = new DenseTensor<float>([b, n, n]);
            var mask = new DenseTensor<float>([b, n]);
            for (int i = 0; i < b; i++)
            {
                var m = chunk[i];
                m.AtomFeatures.CopyTo(atoms.Buffer.Span.Slice(i * n * MolGraphBuilder.NumFeatures));
                for (int a = 0; a < m.NumAtoms; a++) mask[i, a] = 1f;
                foreach (var (src, dst) in m.Edges) adj[i, dst, src] += 1f;
            }

            using var results = _session.Run(
            [
                NamedOnnxValue.CreateFromTensor("spec", specTensor),
                NamedOnnxValue.CreateFromTensor("atoms", atoms),
                NamedOnnxValue.CreateFromTensor("adj", adj),
                NamedOnnxValue.CreateFromTensor("atom_mask", mask),
            ], ["scores"]);
            results.First().AsTensor<float>().ToArray().CopyTo(scores, start);
        }
        return scores;
    }

    /// <summary>
    /// Scores one molecule and returns the peak-to-atom cosine similarity matrix,
    /// row-major [numPeaks, molecule.NumAtoms].
    /// </summary>
    public (float Score, float[] Similarity) Explain(float[] spec, int numPeaks, MolGraph molecule)
    {
        if (numPeaks == 0) throw new ArgumentException("Spectrum has no peaks");
        if (spec.Length != numPeaks * SpectrumFeaturizer.NumFeatures) throw new ArgumentException("spec has wrong length");

        int n = molecule.NumAtoms;
        var specTensor = new DenseTensor<float>(spec, [numPeaks, SpectrumFeaturizer.NumFeatures]);
        var atoms = new DenseTensor<float>(molecule.AtomFeatures.ToArray(), [1, n, MolGraphBuilder.NumFeatures]);
        var adj = new DenseTensor<float>([1, n, n]);
        var mask = new DenseTensor<float>([1, n]);
        for (int a = 0; a < n; a++) mask[0, a] = 1f;
        foreach (var (src, dst) in molecule.Edges) adj[0, dst, src] += 1f;

        using var results = _session.Run(
        [
            NamedOnnxValue.CreateFromTensor("spec", specTensor),
            NamedOnnxValue.CreateFromTensor("atoms", atoms),
            NamedOnnxValue.CreateFromTensor("adj", adj),
            NamedOnnxValue.CreateFromTensor("atom_mask", mask),
        ], ["scores", "similarity"]);
        float score = results.First(r => r.Name == "scores").AsTensor<float>().GetValue(0);
        float[] similarity = results.First(r => r.Name == "similarity").AsTensor<float>().ToArray();
        return (score, similarity);
    }

    public void Dispose() => _session.Dispose();
}

