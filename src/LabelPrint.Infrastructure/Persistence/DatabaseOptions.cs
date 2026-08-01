namespace LabelPrint.Infrastructure.Persistence;

/// <summary>
/// SQLite path and retention options.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "LabelPrint";

    /// <summary>Database file name inside the data directory.</summary>
    public string DatabaseFileName { get; set; } = "labelprint.db";

    /// <summary>Optional absolute override for the database file path.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>How many pre-migration backups to keep.</summary>
    public int BackupRetentionCount { get; set; } = 10;

    /// <summary>
    /// Resolves the absolute SQLite file path.
    /// </summary>
    public string ResolveDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(DatabasePath))
        {
            return DatabasePath;
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro");

        Directory.CreateDirectory(root);
        return Path.Combine(root, DatabaseFileName);
    }

    /// <summary>Backup directory next to the database file.</summary>
    public string ResolveBackupDirectory()
    {
        var dir = Path.Combine(Path.GetDirectoryName(ResolveDatabasePath())!, "backups");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
