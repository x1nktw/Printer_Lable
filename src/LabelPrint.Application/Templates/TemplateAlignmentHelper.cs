namespace LabelPrint.Application.Templates;

/// <summary>
/// Aligns selected template elements within their selection bounds or the canvas.
/// </summary>
public static class TemplateAlignmentHelper
{
    public sealed class MutableBounds
    {
        public required string Id { get; init; }

        public double X { get; set; }

        public double Y { get; set; }

        public required double Width { get; init; }

        public required double Height { get; init; }
    }

    public static void AlignLeft(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var target = selection.Min(b => b.X);
        foreach (var item in selection)
        {
            item.X = target;
        }
    }

    public static void AlignRight(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var target = selection.Max(b => b.X + b.Width);
        foreach (var item in selection)
        {
            item.X = target - item.Width;
        }
    }

    public static void AlignCenterHorizontal(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var minX = selection.Min(b => b.X);
        var maxX = selection.Max(b => b.X + b.Width);
        var center = (minX + maxX) / 2;
        foreach (var item in selection)
        {
            item.X = center - item.Width / 2;
        }
    }

    public static void AlignTop(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var target = selection.Min(b => b.Y);
        foreach (var item in selection)
        {
            item.Y = target;
        }
    }

    public static void AlignBottom(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var target = selection.Max(b => b.Y + b.Height);
        foreach (var item in selection)
        {
            item.Y = target - item.Height;
        }
    }

    public static void AlignCenterVertical(IList<MutableBounds> selection)
    {
        if (selection.Count == 0)
        {
            return;
        }

        var minY = selection.Min(b => b.Y);
        var maxY = selection.Max(b => b.Y + b.Height);
        var center = (minY + maxY) / 2;
        foreach (var item in selection)
        {
            item.Y = center - item.Height / 2;
        }
    }

    public static void AlignToCanvasLeft(IList<MutableBounds> selection) =>
        SetAll(selection, b => b.X = 0, _ => { });

    public static void AlignToCanvasRight(IList<MutableBounds> selection, double canvasWidthMm)
    {
        foreach (var item in selection)
        {
            item.X = canvasWidthMm - item.Width;
        }
    }

    public static void AlignToCanvasCenterHorizontal(IList<MutableBounds> selection, double canvasWidthMm)
    {
        foreach (var item in selection)
        {
            item.X = (canvasWidthMm - item.Width) / 2;
        }
    }

    public static void AlignToCanvasTop(IList<MutableBounds> selection) =>
        SetAll(selection, _ => { }, b => b.Y = 0);

    public static void AlignToCanvasBottom(IList<MutableBounds> selection, double canvasHeightMm)
    {
        foreach (var item in selection)
        {
            item.Y = canvasHeightMm - item.Height;
        }
    }

    public static void AlignToCanvasCenterVertical(IList<MutableBounds> selection, double canvasHeightMm)
    {
        foreach (var item in selection)
        {
            item.Y = (canvasHeightMm - item.Height) / 2;
        }
    }

    private static void SetAll(
        IList<MutableBounds> selection,
        Action<MutableBounds> setX,
        Action<MutableBounds> setY)
    {
        foreach (var item in selection)
        {
            setX(item);
            setY(item);
        }
    }
}
