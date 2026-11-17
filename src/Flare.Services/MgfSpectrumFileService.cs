using Flare.Models.Spectra;
using Flare.Onnx;
using Flare.Services.Abstraction;

namespace Flare.Services;

/// <summary>Reads MGF files via <see cref="MgfReader"/>.</summary>
public sealed class MgfSpectrumFileService : ISpectrumFileService
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".mgf"];

    public Task<IReadOnlyList<SpectrumRecord>> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<SpectrumRecord>>(() =>
            MgfReader.Read(path)
                .Select(s =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ToRecord(s);
                })
                .ToList(), cancellationToken);

    public static SpectrumRecord ToRecord(MgfSpectrum s) =>
        new(s.Index, s.Title, s.Mzs, s.Intensities, s.Metadata)
        {
            PrecursorMz = s.PrecursorMz,
            ParentMass = s.ParentMass,
            Smiles = s.Smiles,
            Formula = s.Formula,
            Adduct = s.Adduct,
            MsLevel = s.MsLevel,
        };
}
