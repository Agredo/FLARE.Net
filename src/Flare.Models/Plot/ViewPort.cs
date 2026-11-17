namespace Flare.Models.Plot;

/// <summary>Visible m/z range of the spectrum plot; the intensity axis always starts at 0.</summary>
public readonly record struct ViewPort(double XMin, double XMax, double YMax = 1.0)
{
    public double XSpan => XMax - XMin;

    /// <summary>Zooms around <paramref name="center"/>; factor &lt; 1 zooms in.</summary>
    public ViewPort Zoom(double center, double factor, ViewPort limits, double minSpan = 0.5)
    {
        double span = Math.Clamp(XSpan * factor, Math.Min(minSpan, limits.XSpan), limits.XSpan);
        double rel = XSpan > 0 ? (center - XMin) / XSpan : 0.5;
        double min = center - rel * span;
        return new ViewPort(min, min + span, YMax).ClampTo(limits);
    }

    public ViewPort Pan(double deltaX, ViewPort limits) =>
        new ViewPort(XMin + deltaX, XMax + deltaX, YMax).ClampTo(limits);

    public ViewPort ClampTo(ViewPort limits)
    {
        double span = Math.Min(XSpan, limits.XSpan);
        double min = Math.Clamp(XMin, limits.XMin, limits.XMax - span);
        return new ViewPort(min, min + span, YMax);
    }
}

/// <summary>Maps data coordinates to pixels inside the padded plot area.</summary>
public sealed class CoordinateTransform(float width, float height, ViewPort viewPort)
{
    public float Width { get; } = width;
    public float Height { get; } = height;
    public ViewPort ViewPort { get; } = viewPort;

    public float PadLeft { get; init; } = 56f;
    public float PadRight { get; init; } = 16f;
    public float PadTop { get; init; } = 28f;
    public float PadBottom { get; init; } = 40f;

    public float PlotWidth => Math.Max(1f, Width - PadLeft - PadRight);
    public float PlotHeight => Math.Max(1f, Height - PadTop - PadBottom);
    public float Bottom => PadTop + PlotHeight;
    public float Right => PadLeft + PlotWidth;

    public float XToPixel(double x) => PadLeft + (float)((x - ViewPort.XMin) / ViewPort.XSpan * PlotWidth);
    public double PixelToX(float px) => ViewPort.XMin + (px - PadLeft) / PlotWidth * ViewPort.XSpan;
    public float YToPixel(double y) => Bottom - (float)(y / ViewPort.YMax * PlotHeight);
    public bool IsInsidePlot(float px, float py) => px >= PadLeft && px <= Right && py >= PadTop && py <= Bottom;
}
