namespace LabelPrint.Application.Icons;

/// <summary>
/// Persists user-hidden icon keys (including built-ins) under LocalAppData.
/// Hidden icons stay out of pickers; templates that already reference them still render.
/// </summary>
public static class HiddenIconStore
{
    private static readonly object Gate = new();
    private static HashSet<string>? _cache;

    private static string StorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "hidden-icons.txt");

    public static IReadOnlySet<string> GetHidden()
    {
        lock (Gate)
        {
            return new HashSet<string>(LoadUnlocked(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool IsHidden(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        lock (Gate)
        {
            return LoadUnlocked().Contains(key.Trim());
        }
    }

    public static void Hide(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lock (Gate)
        {
            var set = LoadUnlocked();
            if (set.Add(key.Trim()))
            {
                SaveUnlocked(set);
            }
        }
    }

    public static void Unhide(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lock (Gate)
        {
            var set = LoadUnlocked();
            if (set.Remove(key.Trim()))
            {
                SaveUnlocked(set);
            }
        }
    }

    private static HashSet<string> LoadUnlocked()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        _cache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = StorePath;
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.Trim();
                    if (t.Length > 0)
                    {
                        _cache.Add(t);
                    }
                }
            }
        }
        catch
        {
            // Best-effort; empty set on read failure.
        }

        return _cache;
    }

    private static void SaveUnlocked(HashSet<string> set)
    {
        _cache = set;
        try
        {
            var path = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, set.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            // Best-effort persistence.
        }
    }
}
