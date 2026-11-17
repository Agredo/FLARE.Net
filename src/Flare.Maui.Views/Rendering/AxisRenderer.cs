using Flare.Models.Plot;
using SkiaSharp;

namespace Flare.Maui.Views.Rendering;

/// <summary>Draws plot frame, ticks, grid and axis titles for a <see cref="CoordinateTransform"/>.</summary>
public static class AxisRenderer
{
    public static void Draw(SKCanvas canvas, CoordinateTransform t, PlotTheme theme, string xTitle, string yTitle)
    {
        using var axisPaint = new SKPaint { Color = theme.Axis, StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var gridPaint = new SKPaint { Color = theme.Grid, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        using var textPaint = new SKPaint { Color = theme.Text, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 11);

        foreach (var x in NiceTicks(t.ViewPort.XMin, t.ViewPort.XMax, Math.Max(2, (int)(t.PlotWidth / 90))))
        {
            float px = t.XToPixel(x);
            canvas.DrawLine(px, t.PadTop, px, t.Bottom, gridPaint);
            canvas.DrawLine(px, t.Bottom, px, t.Bottom + 4, axisPaint);
            canvas.DrawText(FormatTick(x, t.ViewPort.XSpan), px, t.Bottom + 16, SKTextAlign.Center, font, textPaint);
        }

        foreach (var y in NiceTicks(0, Math.Min(1, t.ViewPort.YMax), Math.Max(2, (int)(t.PlotHeight / 50))))
        {
            float py = t.YToPixel(y);
            canvas.DrawLine(t.PadLeft, py, t.Right, py, gridPaint);
            canvas.DrawLine(t.PadLeft - 4, py, t.PadLeft, py, axisPaint);
            canvas.DrawText((y * 100).ToString("0"), t.PadLeft - 7, py + 4, SKTextAlign.Right, font, textPaint);
        }

        canvas.DrawRect(t.PadLeft, t.PadTop, t.PlotWidth, t.PlotHeight, axisPaint);
        canvas.DrawText(xTitle, t.PadLeft + t.PlotWidth / 2, t.Height - 6, SKTextAlign.Center, font, textPaint);

        canvas.Save();
        canvas.RotateDegrees(-90, 14, t.PadTop + t.PlotHeight / 2);
        canvas.DrawText(yTitle, 14, t.PadTop + t.PlotHeight / 2 + 4, SKTextAlign.Center, font, textPaint);
        canvas.Restore();
    }

    /// <summary>Ticks on a 1/2/5 × 10^n grid within [min, max].</summary>
    public static IEnumerable<double> NiceTicks(double min, double max, int maxTicks)
    {
        double span = max - min;
        if (span <= 0 || maxTicks < 1) yield break;
        double raw = span / maxTicks;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double step = mag * (norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10);
        for (double v = Math.Ceiling(min / step) * step; v <= max + step * 1e-9; v += step)
            yield return Math.Abs(v) < step * 1e-9 ? 0 : v;
    }

    private static string FormatTick(double value, double span) =>
        span < 2 ? value.ToString("0.00") : span < 20 ? value.ToString("0.0") : value.ToString("0");
}
