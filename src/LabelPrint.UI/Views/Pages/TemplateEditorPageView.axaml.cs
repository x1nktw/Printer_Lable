using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LabelPrint.UI.ViewModels;
using System.ComponentModel;

namespace LabelPrint.UI.Views.Pages;

public partial class TemplateEditorPageView : UserControl
{
    private CanvasElementViewModel? _dragElement;
    private Point _lastPoint;
    private bool _rubberBandActive;
    private Point _rubberBandStart;
    private Rectangle? _rubberBandRect;

    public TemplateEditorPageView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private TemplateEditorViewModel? Vm => DataContext as TemplateEditorViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        Vm.PropertyChanged += OnViewModelPropertyChanged;
        Vm.SnapGuides.CollectionChanged += (_, _) => RedrawGuides();
        RedrawGrid();
        RedrawGuides();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TemplateEditorViewModel.ShowGrid)
            or nameof(TemplateEditorViewModel.CanvasWidthPx)
            or nameof(TemplateEditorViewModel.CanvasHeightPx)
            or nameof(TemplateEditorViewModel.Zoom)
            or nameof(TemplateEditorViewModel.WidthMm)
            or nameof(TemplateEditorViewModel.HeightMm))
        {
            RedrawGrid();
        }
    }

    private void RedrawGrid()
    {
        var canvas = this.FindControl<Canvas>("GridCanvas");
        if (canvas is null || Vm is null)
        {
            return;
        }

        canvas.Children.Clear();
        if (!Vm.ShowGrid)
        {
            return;
        }

        var width = Vm.CanvasWidthPx;
        var height = Vm.CanvasHeightPx;
        var step = TemplateEditorViewModel.PxPerMm * Vm.Zoom;
        if (step < 2)
        {
            return;
        }

        var brush = new SolidColorBrush(Color.Parse("#E0E0E0"));
        for (var i = 1; i * step < width; i++)
        {
            var x = i * step;
            canvas.Children.Add(new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, height),
                Stroke = brush,
                StrokeThickness = i % 5 == 0 ? 0.8 : 0.4
            });
        }

        for (var i = 1; i * step < height; i++)
        {
            var y = i * step;
            canvas.Children.Add(new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(width, y),
                Stroke = brush,
                StrokeThickness = i % 5 == 0 ? 0.8 : 0.4
            });
        }
    }

    private void RedrawGuides()
    {
        var canvas = this.FindControl<Canvas>("GuideCanvas");
        if (canvas is null || Vm is null)
        {
            return;
        }

        canvas.Children.Clear();
        var brush = new SolidColorBrush(Color.Parse("#E81123"));
        var width = Vm.CanvasWidthPx;
        var height = Vm.CanvasHeightPx;

        foreach (var guide in Vm.SnapGuides)
        {
            if (guide.IsVertical)
            {
                canvas.Children.Add(new Line
                {
                    StartPoint = new Point(guide.PositionPx, 0),
                    EndPoint = new Point(guide.PositionPx, height),
                    Stroke = brush,
                    StrokeThickness = 1,
                    Opacity = 0.85
                });
            }
            else
            {
                canvas.Children.Add(new Line
                {
                    StartPoint = new Point(0, guide.PositionPx),
                    EndPoint = new Point(width, guide.PositionPx),
                    Stroke = brush,
                    StrokeThickness = 1,
                    Opacity = 0.85
                });
            }
        }
    }

    private void OnElementPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null || Vm.IsPreviewMode)
        {
            return;
        }

        if (sender is not Control control || control.Tag is not CanvasElementViewModel element)
        {
            return;
        }

        var addToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        Vm.SelectElement(element, addToSelection);
        _dragElement = element;
        Vm.BeginDrag(element);
        _lastPoint = e.GetPosition(this.FindControl<Grid>("DesignSurfaceHost")!);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null || Vm.IsPreviewMode || e.Source is Control { Tag: CanvasElementViewModel })
        {
            return;
        }

        var host = this.FindControl<Grid>("DesignSurfaceHost");
        if (host is null)
        {
            return;
        }

        var point = e.GetPosition(host);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Vm.ClearSelection();
        }

        _rubberBandActive = true;
        _rubberBandStart = point;
        _rubberBandRect = new Rectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 1,
            StrokeDashArray = [4, 2],
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_rubberBandRect, point.X);
        Canvas.SetTop(_rubberBandRect, point.Y);
        this.FindControl<Canvas>("RubberBandCanvas")?.Children.Add(_rubberBandRect);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var host = this.FindControl<Grid>("DesignSurfaceHost");
        if (host is null)
        {
            return;
        }

        var point = e.GetPosition(host);

        if (_rubberBandActive && _rubberBandRect is not null)
        {
            var x = Math.Min(_rubberBandStart.X, point.X);
            var y = Math.Min(_rubberBandStart.Y, point.Y);
            var w = Math.Abs(point.X - _rubberBandStart.X);
            var h = Math.Abs(point.Y - _rubberBandStart.Y);
            Canvas.SetLeft(_rubberBandRect, x);
            Canvas.SetTop(_rubberBandRect, y);
            _rubberBandRect.Width = w;
            _rubberBandRect.Height = h;
            return;
        }

        if (_dragElement is null || Vm is null || !e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var dx = point.X - _lastPoint.X;
        var dy = point.Y - _lastPoint.Y;
        _lastPoint = point;
        Vm.DragMove(_dragElement, dx, dy);
        RedrawGuides();
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var host = this.FindControl<Grid>("DesignSurfaceHost");
        if (_rubberBandActive && host is not null && Vm is not null && _rubberBandRect is not null)
        {
            var point = e.GetPosition(host);
            var addToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
            Vm.SelectInRect(_rubberBandStart.X, _rubberBandStart.Y, point.X, point.Y, addToSelection);
            this.FindControl<Canvas>("RubberBandCanvas")?.Children.Clear();
            _rubberBandRect = null;
            _rubberBandActive = false;
        }

        if (_dragElement is not null && Vm is not null)
        {
            Vm.EndDrag();
            RedrawGuides();
        }

        _dragElement = null;
        e.Pointer.Capture(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
        {
            if (Vm.UndoCommand.CanExecute(null))
            {
                Vm.UndoCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
        {
            if (Vm.RedoCommand.CanExecute(null))
            {
                Vm.RedoCommand.Execute(null);
            }

            e.Handled = true;
        }
    }
}
