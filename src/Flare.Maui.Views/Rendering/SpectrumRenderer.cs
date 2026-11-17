using Flare.Models.Plot;
using Flare.ViewModels;
using SkiaSharp;

namespace Flare.Maui.Views.Rendering;

/// <summary>Colours for the Skia plots (dark theme, matching the app styles).</summary>
public sealed record PlotTheme(SKColor Background, SKColor Axis, SKColor Grid, SKColor Text, SKColor RawPeak,
    SKColor Precursor, SKColor Selection)
{
    public static PlotTheme Dark { get; } = new(
        new SKColor(0x1A, 0x1A, 0x2E), new SKColor(0x8A, 0x8A, 0xA8), new SKColor(0x2A, 0x2A, 0x44),
        new SKColor(0xC8, 0xC8, 0xD8), new SKColor(0x5A, 0x5A, 0x78), new SKColor(0xFF, 0x6B, 0x6B), SKColors.White);
}

/// <summary>
/// Stick spectrum: raw peaks in grey, FLARE model peaks coloured by their highlight weight,
/// precursor as dashed line and labels for the hovered/selected peak.
/// </summary>
public static class SpectrumRenderer
{
    public const float HitTolerancePixels = 6f;

    public static CoordinateTransform CreateTransform(float width, float height, ViewPort viewPort) =>
        new(width, height, viewPort) { PadLeft = 52, PadRight = 16, PadTop = 26, PadBottom = 38 };

    public static void Draw(SKCanvas canvas, float width, float height, SpectrumViewModel vm, PlotTheme theme)
    {
        canvas.Clear(theme.Background);
        var t = CreateTransform(width, height, vm.ViewPort);
        AxisRenderer.Draw(canvas, t, theme, "m/z", "Rel. Intensität (%)");

        using var font = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint { Color = theme.Text, IsAntialias = true };
        if (vm.RawPeaks.Count == 0)
        {
            canvas.DrawText("Keine Peaks – MGF-Datei öffnen", width / 2, height / 2, SKTextAlign.Center, font, textPaint);
            return;
        }

        canvas.Save();
        canvas.ClipRect(new SKRect(t.PadLeft, t.PadTop - 2, t.Right, t.Bottom));

        using var rawPaint = new SKPaint { Color = theme.RawPeak, StrokeWidth = 1.2f, IsAntialias = true };
        foreach (var p in vm.RawPeaks)
        {
            if (p.Mz < vm.ViewPort.XMin || p.Mz > vm.ViewPort.XMax) continue;
            float x = t.XToPixel(p.Mz);
            canvas.DrawLine(x, t.Bottom, x, t.YToPixel(p.RelativeIntensity), rawPaint);
        }

        if (vm.PrecursorMz is double prec)
        {
            using var precPaint = new SKPaint
            {
                Color = theme.Precursor, StrokeWidth = 1, IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([5, 4], 0),
            };
            float x = t.XToPixel(prec);
            canvas.DrawLine(x, t.PadTop, x, t.Bottom, precPaint);
        }

        using var peakPaint = new SKPaint { StrokeWidth = 2.4f, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        PeakRowViewModel? selected = null, hovered = null;
        // draw weak peaks first so strongly highlighted ones end up on top
        foreach (var p in vm.Peaks.Where(p => !p.IsPrecursor).OrderBy(p => p.Weight))
        {
            if (p.Index == vm.SelectedPeakIndex) selected = p;
            if (p.Index == vm.HoveredPeakIndex) hovered = p;
            if (p.Mz < vm.ViewPort.XMin || p.Mz > vm.ViewPort.XMax) continue;
            float x = t.XToPixel(p.Mz);
            peakPaint.Color = ToSk(p);
            peakPaint.StrokeWidth = p.IsHighlighted ? 2.4f : 1.4f;
            canvas.DrawLine(x, t.Bottom, x, t.YToPixel(p.PlotIntensity), peakPaint);
        }
        canvas.Restore();

        if (hovered != null && hovered != selected) DrawMarker(canvas, t, hovered, theme, font, emphasize: false);
        if (selected != null) DrawMarker(canvas, t, selected, theme, font, emphasize: true);
    }

    private static void DrawMarker(SKCanvas canvas, CoordinateTransform t, PeakRowViewModel p, PlotTheme theme, SKFont font,
        bool emphasize)
    {
        if (p.Mz < t.ViewPort.XMin || p.Mz > t.ViewPort.XMax) return;
        float x = t.XToPixel(p.Mz), top = t.YToPixel(p.PlotIntensity);
        using var outline = new SKPaint
        {
            Color = emphasize ? theme.Selection : theme.Selection.WithAlpha(150),
            StrokeWidth = emphasize ? 5 : 4, IsAntialias = true, StrokeCap = SKStrokeCap.Round,
        };
        using var inner = new SKPaint { Color = ToSk(p), StrokeWidth = 2.4f, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(x, t.Bottom, x, top, outline);
        canvas.DrawLine(x, t.Bottom, x, top, inner);

        string label = $"{p.MzText}  {p.Formula}";
        float w = font.MeasureText(label);
        float lx = Math.Clamp(x - w / 2, t.PadLeft + 2, t.Right - w - 2);
        float ly = Math.Max(t.PadTop + 12, top - 8);
        using var bg = new SKPaint { Color = theme.Background.WithAlpha(220) };
        canvas.DrawRect(lx - 4, ly - 12, w + 8, 16, bg);
        using var text = new SKPaint { Color = theme.Selection, IsAntialias = true };
        canvas.DrawText(label, lx, ly, SKTextAlign.Left, font, text);
    }

    private static SKColor ToSk(WeightedRowViewModel row) => new(row.Color.R, row.Color.G, row.Color.B);
}

