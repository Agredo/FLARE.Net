using Flare.Services.Abstraction;

namespace Flare.Maui.Services;

/// <summary>File picker based on MAUI Essentials <see cref="FilePicker"/>.</summary>
public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<string?> PickFileAsync(string title, IReadOnlyList<string> extensions)
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = extensions,
            [DevicePlatform.MacCatalyst] = extensions.Select(e => e.TrimStart('.')),
            // Android filters by MIME type and has none for .mgf/.onnx, so let the user pick any file
            [DevicePlatform.Android] = new[] { "*/*" },
        });
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = types });
        return result?.FullPath;
    }
}
