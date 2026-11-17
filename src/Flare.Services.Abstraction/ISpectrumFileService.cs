using Flare.Models.Spectra;

namespace Flare.Services.Abstraction;

public interface ISpectrumFileService
{
    /// <summary>File extensions (with dot) that <see cref="LoadAsync"/> can read.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<IReadOnlyList<SpectrumRecord>> LoadAsync(string path, CancellationToken cancellationToken = default);
}
