namespace LabelPrint.Application.Paths;

/// <summary>
/// User-owned folders outside the Velopack install directory.
/// </summary>
public static class UserDataPaths
{
    public const string AppFolderName = "LabelPrint Pro";

    /// <summary>Default template export folder in Documents (survives Velopack reinstall).</summary>
    public static string ResolveDefaultExportsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            AppFolderName,
            "exports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Legacy export folder under LocalAppData (pre-1.0.1).</summary>
    public static string ResolveLegacyExportsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "exports");

    /// <summary>
    /// Copies JSON exports from the legacy AppData folder into Documents when present.
    /// </summary>
    public static int MigrateLegacyExportsIfNeeded()
    {
        var legacyDir = ResolveLegacyExportsDirectory();
        if (!Directory.Exists(legacyDir))
        {
            return 0;
        }

        var targetDir = ResolveDefaultExportsDirectory();
        var migrated = 0;

        foreach (var file in Directory.EnumerateFiles(legacyDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(targetDir, name);
            if (File.Exists(dest))
            {
                var stem = Path.GetFileNameWithoutExtension(name);
                dest = Path.Combine(targetDir, $"{stem}_legacy{Path.GetExtension(name)}");
            }

            File.Copy(file, dest, overwrite: false);
            migrated++;
        }

        return migrated;
    }

    public static string BuildTemplateExportFileName(string templateName) =>
        $"template_{SanitizeFileName(templateName)}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
