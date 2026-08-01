namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Paths for the development order inbox (file-based provider).
/// </summary>
public static class OrderInboxPaths
{
    /// <summary>Root folder: %LocalAppData%\LabelPrintPro\orders-inbox</summary>
    public static string InboxDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "orders-inbox");

    /// <summary>Processed files are moved here after sync.</summary>
    public static string ProcessedDirectory => Path.Combine(InboxDirectory, "processed");

    /// <summary>Ensures inbox and processed folders exist.</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(InboxDirectory);
        Directory.CreateDirectory(ProcessedDirectory);
    }
}
