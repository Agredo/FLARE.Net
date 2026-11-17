using System.Collections.Concurrent;
using Flare.Services.Abstraction;

namespace Flare.Services;

/// <summary>Non-persistent settings store (tests, headless use).</summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, object?> _values = new();

    public T Get<T>(string key, T defaultValue) =>
        _values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;

    public void Set<T>(string key, T value) => _values[key] = value;
}
