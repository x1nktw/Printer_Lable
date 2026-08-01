namespace LabelPrint.Application.Templates;

/// <summary>
/// Computes alignment guide lines shown while dragging with snap enabled.
/// </summary>
public static class TemplateSnapGuides
{
    public sealed record GuideLine(double PositionPx, bool IsVertical);

    public sealed record ElementRect(double X, double Y, double Width, double Height);

    /// <summary>
    /// Returns guide lines when element edges are near canvas edges/center or other elements.
    /// </summary>
    public static IReadOnlyList<GuideLine> ComputeGuides(
        ElementRect dragged,
        double canvasWidthMm,
        double canvasHeightMm,
        double pxPerMm,
        double zoom,
        IEnumerable<ElementRect> others,
        double thresholdMm = 0.5)
    {
        var scale = pxPerMm * zoom;
        var guides = new List<GuideLine>();
        var edgesX = new[] { 0d, canvasWidthMm / 2, canvasWidthMm };
        var edgesY = new[] { 0d, canvasHeightMm / 2, canvasHeightMm };

        var dragLeft = dragged.X;
        var dragRight = dragged.X + dragged.Width;
        var dragCenterX = dragged.X + dragged.Width / 2;
        var dragTop = dragged.Y;
        var dragBottom = dragged.Y + dragged.Height;
        var dragCenterY = dragged.Y + dragged.Height / 2;

        foreach (var edge in edgesX)
        {
            if (Near(dragLeft, edge, thresholdMm) || Near(dragRight, edge, thresholdMm) || Near(dragCenterX, edge, thresholdMm))
            {
                guides.Add(new GuideLine(edge * scale, true));
            }
        }

        foreach (var edge in edgesY)
        {
            if (Near(dragTop, edge, thresholdMm) || Near(dragBottom, edge, thresholdMm) || Near(dragCenterY, edge, thresholdMm))
            {
                guides.Add(new GuideLine(edge * scale, false));
            }
        }

        foreach (var other in others)
        {
            var otherLeft = other.X;
            var otherRight = other.X + other.Width;
            var otherCenterX = other.X + other.Width / 2;
            var otherTop = other.Y;
            var otherBottom = other.Y + other.Height;
            var otherCenterY = other.Y + other.Height / 2;

            foreach (var refX in new[] { otherLeft, otherRight, otherCenterX })
            {
                if (Near(dragLeft, refX, thresholdMm) || Near(dragRight, refX, thresholdMm) || Near(dragCenterX, refX, thresholdMm))
                {
                    guides.Add(new GuideLine(refX * scale, true));
                }
            }

            foreach (var refY in new[] { otherTop, otherBottom, otherCenterY })
            {
                if (Near(dragTop, refY, thresholdMm) || Near(dragBottom, refY, thresholdMm) || Near(dragCenterY, refY, thresholdMm))
                {
                    guides.Add(new GuideLine(refY * scale, false));
                }
            }
        }

        return guides
            .DistinctBy(g => (g.PositionPx, g.IsVertical))
            .ToList();
    }

    private static bool Near(double a, double b, double threshold) => Math.Abs(a - b) <= threshold;
}
