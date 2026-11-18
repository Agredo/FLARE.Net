using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Flare.ViewModels.Localization;

/// <summary>Localized UI strings. Changing the language notifies bindings that use <see cref="this[string]"/>.</summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    string Language { get; }
    string this[string key] { get; }
    void SetLanguage(string language);
}

/// <summary>Resource-backed German and English application text.</summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resources;

    public LocalizationService() : this(Strings.ResourceManager) { }

    internal LocalizationService(ResourceManager resources)
    {
        _resources = resources;
        SetCulture(language: null);
    }

    public string Language { get; private set; } = "de";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public void SetLanguage(string language)
    {
        string selected = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
        bool changed = !string.Equals(selected, Language, StringComparison.Ordinal);

        // The culture is process wide, so it is always re-applied even when this instance
        // already sits on the requested language: another instance may have reset it.
        SetCulture(selected);
        if (!changed) return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private void SetCulture(string? language)
    {
        Language = language ?? "de";
        CultureInfo culture = CultureInfo.GetCultureInfo(Language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
