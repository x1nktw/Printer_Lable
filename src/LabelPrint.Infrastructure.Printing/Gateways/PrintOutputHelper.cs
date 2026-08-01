using LabelPrint.Domain.Entities;
using LabelPrint.Infrastructure.Printing.Options;
using Microsoft.Extensions.Options;

namespace LabelPrint.Infrastructure.Printing.Gateways;

internal static class PrintOutputHelper
{
    public static string ResolveDirectory(Printer printer, IOptions<PrintingOptions> options)
    {
        if (!string.IsNullOrWhiteSpace(printer.ConnectionString))
        {
            return Environment.ExpandEnvironmentVariables(printer.ConnectionString.Trim());
        }

        return options.Value.ResolveOutputDirectory();
    }

    public static string CreateTimestampedPath(string directory, string prefix, string extension)
    {
        Directory.CreateDirectory(directory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        return Path.Combine(directory, $"{prefix}_{stamp}{extension}");
    }
}
