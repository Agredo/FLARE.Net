using Flare.ViewModels.Localization;

namespace Flare.ViewModels.Tests;

[Collection("Localization")]
public sealed class LocalizationServiceTests
{
    [Fact]
    public void DefaultsToGermanAndFallsBackForUnsupportedLanguage()
    {
        var localization = new LocalizationService();
        Assert.Equal("de", localization.Language);
        Assert.Equal("Einstellungen", localization["Settings"]);

        localization.SetLanguage("fr");

        Assert.Equal("de", localization.Language);
        Assert.Equal("Einstellungen", localization["Settings"]);
    }

    [Fact]
    public void EnglishSelection_NotifiesBindingsAndLocalizesValidation()
    {
        var localization = new LocalizationService();
        var notifications = new List<string?>();
        localization.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        localization.SetLanguage("en");

        Assert.Equal("en", localization.Language);
        Assert.Equal("Settings", localization["Settings"]);
        Assert.Contains("Item[]", notifications);
        var exception = Assert.Throws<FormatException>(() => ManualSpectrumViewModel.Parse("", ""));
        Assert.Equal("No m/z values entered.", exception.Message);
    }

    [Fact]
    public void ChangingLanguage_RefreshesMultiArgumentValidation()
    {
        var localization = new LocalizationService();
        var spectrum = new ManualSpectrumViewModel { MzValues = "100; 200", IntensityValues = "50" };
        Assert.False(spectrum.TryGetPeaks(out _));

        localization.SetLanguage("en");
        spectrum.RefreshText();

        Assert.Equal("2 m/z values, but 1 intensities.", spectrum.ErrorMessage);
    }
}
