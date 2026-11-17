using Flare.Models.Plot;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Flare.Maui.Views.Controls;

/// <summary>Horizontal colour bar (0 … 1) for the selected colormap.</summary>
public sealed class ColormapBar : ContentView
{
    public static readonly BindableProperty ColormapProperty =
        BindableProperty.Create(nameof(Colormap), typeof(ColormapType), typeof(ColormapBar), ColormapType.Viridis,
            propertyChanged: (b, _, _) => ((ColormapBar)b)._canvas.InvalidateSurface());

    private readonly SKCanvasView _canvas;

    public ColormapBar()
    {
        _canvas = new SKCanvasView { IgnorePixelScaling = true };
        _canvas.PaintSurface += OnPaintSurface;
        Content = _canvas;
        HeightRequest = 14;
    }

    public ColormapType Colormap
    {
        get => (ColormapType)GetValue(ColormapProperty);
        set => SetValue(ColormapProperty, value);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        int w = e.Info.Width, h = e.Info.Height;
        if (w <= 0) return;
        using var paint = new SKPaint();
        for (int x = 0; x < w; x++)
        {
            var c = Colormaps.Evaluate(Colormap, w == 1 ? 0 : x / (double)(w - 1));
            paint.Color = new SKColor(c.R, c.G, c.B);
            canvas.DrawRect(x, 0, 1, h, paint);
        }
    }
}
