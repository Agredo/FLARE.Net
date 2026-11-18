namespace Flare.Services.Abstraction;

/// <summary>Persistent key/value settings (e.g. MAUI Preferences).</summary>
public interface ISettingsStore
{
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
}

public static class SettingsKeys
{
    public const string ModelPath = "flare.modelPath";
    public const string Normalization = "flare.normalization";
    public const string Colormap = "flare.colormap";
    public const string SoftmaxTemperature = "flare.softmaxTemperature";
    public const string HighlightThreshold = "flare.highlightThreshold";
    public const string Language = "flare.language";
}

