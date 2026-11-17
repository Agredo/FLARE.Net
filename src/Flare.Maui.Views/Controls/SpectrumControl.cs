using System.ComponentModel;
using Flare.Maui.Views.Rendering;
using Flare.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Flare.Maui.Views.Controls;

/// <summary>
/// Interactive stick spectrum. Click selects a peak, hover highlights, wheel zooms, drag pans,
/// double-click resets. All state lives in <see cref="SpectrumViewModel"/>.
/// </summary>
public sealed class SpectrumControl : ContentView
{
    public static readonly BindableProperty ViewModelProperty =
        BindableProperty.Create(nameof(ViewModel), typeof(SpectrumViewModel), typeof(SpectrumControl),
            propertyChanged: OnViewModelChanged);

    private const float DragThreshold = 4f;
    private const double WheelZoomFactor = 0.85;

    private readonly SKCanvasView _canvas;
    private SKPoint? _pressPoint;
    private SKPoint _lastPoint;
    private bool _dragging;
    private DateTime _lastClick;

    public SpectrumControl()
    {
        _canvas = new SKCanvasView { IgnorePixelScaling = true, EnableTouchEvents = true };
        _canvas.PaintSurface += OnPaintSurface;
        _canvas.Touch += OnTouch;
        Content = _canvas;
    }

    public SpectrumViewModel? ViewModel
    {
        get => (SpectrumViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var size = e.Info;
        if (ViewModel is { } vm) SpectrumRenderer.Draw(e.Surface.Canvas, size.Width, size.Height, vm, PlotTheme.Dark);
        else e.Surface.Canvas.Clear(PlotTheme.Dark.Background);
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null) return;
        var t = SpectrumRenderer.CreateTransform((float)_canvas.Width, (float)_canvas.Height, vm.ViewPort);
        double mz = t.PixelToX(e.Location.X);
        double tolerance = SpectrumRenderer.HitTolerancePixels / t.PlotWidth * vm.ViewPort.XSpan;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _pressPoint = _lastPoint = e.Location;
                _dragging = false;
                break;

            case SKTouchAction.Moved when e.InContact && _pressPoint is { } start:
                if (!_dragging && SKPoint.Distance(start, e.Location) > DragThreshold) _dragging = true;
                if (_dragging)
                {
                    vm.PanBy(-(e.Location.X - _lastPoint.X) / t.PlotWidth * vm.ViewPort.XSpan);
                    _lastPoint = e.Location;
                }
                break;

            case SKTouchAction.Moved:
                vm.HoverAt(t.IsInsidePlot(e.Location.X, e.Location.Y) ? mz : null, tolerance);
                break;

            case SKTouchAction.Released:
                if (!_dragging && _pressPoint is not null && t.IsInsidePlot(e.Location.X, e.Location.Y))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastClick).TotalMilliseconds < 350) vm.ResetZoomCommand.Execute(null);
                    else vm.SelectAt(mz, tolerance);
                    _lastClick = now;
                }
                _pressPoint = null;
                _dragging = false;
                break;

            case SKTouchAction.WheelChanged:
                vm.ZoomAt(mz, e.WheelDelta > 0 ? WheelZoomFactor : 1 / WheelZoomFactor);
                break;

            case SKTouchAction.Exited:
            case SKTouchAction.Cancelled:
                vm.HoverAt(null, tolerance);
                _pressPoint = null;
                _dragging = false;
                break;
        }
        e.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpectrumViewModel.RenderVersion) or nameof(SpectrumViewModel.ViewPort))
            _canvas.InvalidateSurface();
    }

    private static void OnViewModelChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var control = (SpectrumControl)bindable;
        if (oldValue is SpectrumViewModel oldVm) oldVm.PropertyChanged -= control.OnViewModelPropertyChanged;
        if (newValue is SpectrumViewModel newVm) newVm.PropertyChanged += control.OnViewModelPropertyChanged;
        control._canvas.InvalidateSurface();
    }
}

