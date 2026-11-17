using Flare.Services.Abstraction;

namespace Flare.Maui.Services;

/// <summary>MAUI Essentials <see cref="Preferences"/>-backed settings store.</summary>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    public T Get<T>(string key, T defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void Set<T>(string key, T value) => Preferences.Default.Set(key, value);
}
