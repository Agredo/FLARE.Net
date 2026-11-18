using System.ComponentModel;
using Flare.ViewModels.Localization;

namespace Flare.Maui.Views.Localization;

/// <summary>Holds the active localization service and forwards language changes on the UI thread.</summary>
public static class LocalizationProvider
{
    private static ILocalizationService? _current;

    // Created lazily so the design-time fallback never overwrites the culture applied at startup.
    public static ILocalizationService Current
    {
        get => _current ??= new LocalizationService();
        set
        {
            if (ReferenceEquals(_current, value)) return;
            if (_current is not null) _current.PropertyChanged -= OnLanguageChanged;
            _current = value;
            _current.PropertyChanged += OnLanguageChanged;
            Events.HandleEvent(null, EventArgs.Empty, nameof(LanguageChanged));
        }
    }

    public static event EventHandler LanguageChanged
    {
        add => Events.AddEventHandler(value);
        remove => Events.RemoveEventHandler(value);
    }

    private static readonly WeakEventManager Events = new();

    private static void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ILocalizationService.Language))
            MainThread.BeginInvokeOnMainThread(() => Events.HandleEvent(null, EventArgs.Empty, nameof(LanguageChanged)));
    }
}
