using Avalonia.Controls;
using Avalonia.Threading;

namespace LabelPrint.UI.Views.Pages;

public partial class OrdersPageView : UserControl
{
    private readonly HashSet<DataGrid> _headerFloorsApplied = new();

    public OrdersPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            OrdersGrid.LayoutUpdated += OnGridLayoutUpdated;
            ItemsGrid.LayoutUpdated += OnGridLayoutUpdated;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            OrdersGrid.LayoutUpdated -= OnGridLayoutUpdated;
            ItemsGrid.LayoutUpdated -= OnGridLayoutUpdated;
            _headerFloorsApplied.Clear();
        };
    }

    private void OnGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not DataGrid grid || _headerFloorsApplied.Contains(grid))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => TryApplyHeaderFloors(grid), DispatcherPriority.Background);
    }

    private void TryApplyHeaderFloors(DataGrid grid)
    {
        if (_headerFloorsApplied.Contains(grid))
        {
            return;
        }

        var sized = grid.Columns.Where(c => !c.Width.IsStar).ToList();
        if (sized.Count == 0 || sized.Any(c => c.ActualWidth <= 0))
        {
            return;
        }

        foreach (var column in sized)
        {
            // Floor at measured header width so columns cannot shrink below the caption.
            column.MinWidth = Math.Max(column.MinWidth, column.ActualWidth);
        }

        _headerFloorsApplied.Add(grid);
    }
}
