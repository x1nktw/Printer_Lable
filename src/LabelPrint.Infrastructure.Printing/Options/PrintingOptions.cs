namespace LabelPrint.Infrastructure.Printing.Options;

/// <summary>
/// Configuration for virtual/file printing output paths.
/// </summary>
public sealed class PrintingOptions
{
    public const string SectionName = "Printing";

    /// <summary>Optional override for file/virtual printer output. Defaults to LocalAppData\LabelPrintPro\prints.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Resolves the directory used by File and TSPL gateways when ConnectionString is empty.</summary>
    public string ResolveOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(OutputDirectory))
        {
            return Environment.ExpandEnvironmentVariables(OutputDirectory.Trim());
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "prints");
    }
}
