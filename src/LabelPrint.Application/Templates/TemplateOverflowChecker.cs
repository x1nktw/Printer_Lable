namespace LabelPrint.Application.Templates;

/// <summary>
/// Detects template elements that extend beyond the canvas bounds.
/// </summary>
public static class TemplateOverflowChecker
{
    public sealed record ElementRect(string Id, double X, double Y, double Width, double Height);

    /// <summary>Returns true when any edge of the element exceeds the canvas.</summary>
    public static bool IsOverflow(double x, double y, double width, double height, double canvasWidthMm, double canvasHeightMm)
    {
        const double tolerance = 0.01;
        return x < -tolerance
               || y < -tolerance
               || x + width > canvasWidthMm + tolerance
               || y + height > canvasHeightMm + tolerance;
    }

    /// <summary>Returns ids of elements that overflow the canvas.</summary>
    public static IReadOnlyList<string> GetOverflowElementIds(
        IEnumerable<ElementRect> elements,
        double canvasWidthMm,
        double canvasHeightMm)
    {
        return elements
            .Where(e => IsOverflow(e.X, e.Y, e.Width, e.Height, canvasWidthMm, canvasHeightMm))
            .Select(e => e.Id)
            .ToList();
    }

    /// <summary>Builds a short status message for overflow warnings.</summary>
    public static string? BuildStatusMessage(
        IEnumerable<ElementRect> elements,
        double canvasWidthMm,
        double canvasHeightMm)
    {
        var ids = GetOverflowElementIds(elements, canvasWidthMm, canvasHeightMm);
        if (ids.Count == 0)
        {
            return null;
        }

        return ids.Count == 1
            ? "Предупреждение: 1 элемент выходит за границы холста"
            : $"Предупреждение: {ids.Count} элементов выходят за границы холста";
    }
}
