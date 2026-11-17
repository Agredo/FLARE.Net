using System.Globalization;

namespace Flare.Maui.Views.Converters;

/// <summary>Converts "#RRGGBB" strings from view models to <see cref="Color"/>.</summary>
public sealed class HexToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && Color.TryParse(s, out var c) ? c : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bold font for selected rows.</summary>
public sealed class BoolToFontAttributesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontAttributes.Bold : FontAttributes.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverts a boolean (e.g. IsBusy → IsEnabled).</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Maps the app's <see cref="Flare.Models.Plot.ColormapType"/> to the MolDraw colormap.</summary>
public sealed class ColormapTypeToMolDrawConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Flare.Models.Plot.ColormapType.Plasma => MolDraw.Skia.Colormap.Plasma,
        Flare.Models.Plot.ColormapType.Inferno => MolDraw.Skia.Colormap.Inferno,
        Flare.Models.Plot.ColormapType.Magma => MolDraw.Skia.Colormap.Magma,
        Flare.Models.Plot.ColormapType.Turbo => MolDraw.Skia.Colormap.Turbo,
        _ => MolDraw.Skia.Colormap.Viridis,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the value is not null (and not an empty string).</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? s.Length > 0 : value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
