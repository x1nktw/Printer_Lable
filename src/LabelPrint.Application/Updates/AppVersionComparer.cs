namespace LabelPrint.Application.Updates;

/// <summary>Helpers for comparing LabelPrint product versions (SemVer core).</summary>
public static class AppVersionComparer
{
    public static string Normalize(string tagOrVersion)
    {
        var s = tagOrVersion.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            s = s[1..];
        }

        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            s = s[..dash];
        }

        return s;
    }

    public static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(Pad(Normalize(latest)), out var latestV))
        {
            return false;
        }

        if (!Version.TryParse(Pad(Normalize(current)), out var currentV))
        {
            return true;
        }

        return latestV > currentV;
    }

    private static string Pad(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        while (parts.Count < 3)
        {
            parts.Add("0");
        }

        return string.Join('.', parts.Take(4));
    }
}
