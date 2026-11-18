using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Flare.Maui.Views.Localization;
using Flare.ViewModels.Localization;

namespace Flare.Maui.Views.Markup;

/// <summary>
/// XAML markup extension for localized text: <c>{m:Translate OpenMgf}</c>. The key doubles as the
/// resx entry name, so adding a string needs no C# change. Bound controls refresh whenever the
/// language changes.
/// </summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    private static readonly ResourceManager Resources =
        new("Flare.ViewModels.Localization.Strings", typeof(ILocalizationService).Assembly);

    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => Create(Key);

    /// <summary>The same binding for UI that is built in C# rather than XAML.</summary>
    public static BindingBase Create(string key) => new Binding
    {
        Path = nameof(TranslationSource.Value),
        Source = new TranslationSource(key),
        Mode = BindingMode.OneWay,
    };

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);

    private sealed class TranslationSource : INotifyPropertyChanged
    {
        private readonly string _key;

        public TranslationSource(string key)
        {
            _key = key;
            LocalizationProvider.LanguageChanged += OnLanguageChanged;
        }

        public string Value => Resources.GetString(_key, CultureInfo.CurrentUICulture) ?? _key;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnLanguageChanged(object? sender, EventArgs e) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}
