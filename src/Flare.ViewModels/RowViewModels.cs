using CommunityToolkit.Mvvm.ComponentModel;
using Flare.Models.Analysis;
using Flare.Models.Plot;

namespace Flare.ViewModels;

/// <summary>Base class for table rows that carry a highlight weight and its colormap colour.</summary>
public abstract partial class WeightedRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightText))]
    private float _weight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private RgbColor _color = new(0x80, 0x80, 0x80);

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>False when the weight is below the highlight threshold; the row is then shown neutral.</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    public static RgbColor NeutralColor { get; } = new(0x5A, 0x5A, 0x78);

    public string ColorHex => Color.ToHex();
    public string WeightText => Weight.ToString("0.00");

    public void ApplyWeight(float weight, ColormapType colormap, float threshold = 0f)
    {
        Weight = weight;
        IsHighlighted = weight >= threshold;
        Color = IsHighlighted ? Colormaps.Evaluate(colormap, weight) : NeutralColor;
    }
}

/// <summary>One model peak (subformula-annotated) of the analysed spectrum.</summary>
public sealed class PeakRowViewModel(ExplainedPeak peak) : WeightedRowViewModel
{
    public ExplainedPeak Peak { get; } = peak;
    public int Index => Peak.Index;
    public double Mz => Peak.Mz;
    public string Formula => Peak.Formula;
    public bool IsPrecursor => Peak.IsPrecursor;
    public string MzText => Peak.Mz.ToString("0.0000");
    public string IntensityText => Peak.RelativeIntensity is double r ? (r * 100).ToString("0.0") : "–";

    /// <summary>Height used for plotting: raw relative intensity, falling back to the model intensity.</summary>
    public double PlotIntensity => Math.Clamp(Peak.RelativeIntensity ?? Peak.ModelIntensity, 0, 1);
}

/// <summary>One heavy atom of the analysed molecule (RDKit / FLARE atom order).</summary>
public sealed class AtomRowViewModel(int index, string symbol) : WeightedRowViewModel
{
    public int Index { get; } = index;
    public string Symbol { get; } = symbol;
    public string Label => $"{Symbol}{Index}";
}
