namespace Flare.Services.Abstraction;

public interface IFilePickerService
{
    /// <summary>Shows a file-open dialog; returns null when cancelled.</summary>
    /// <param name="extensions">Allowed extensions including the dot, e.g. ".mgf".</param>
    Task<string?> PickFileAsync(string title, IReadOnlyList<string> extensions);
}
