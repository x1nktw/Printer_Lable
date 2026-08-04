using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Marking;

/// <summary>
/// Root/subcategory names and helpers for the marking (маркировка) catalog section.
/// </summary>
public static class MarkingCategories
{
    public const string Raw = "Сырьё";
    public const string Prep = "Заготовки";
    public const string SemiFinished = "Полуфабрикаты";
    public const string Sauces = "Соусы";

    public static readonly string[] Roots =
    [
        Raw,
        Prep,
        SemiFinished,
        Sauces
    ];

    /// <summary>No longer seeded — users create their own. Kept empty on purpose.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultSubcategories =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Names previously seeded; archived on upgrade so the catalog starts clean.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> LegacyDefaultSubcategories =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Raw] = ["Мясо", "Овощи", "Сыр", "Выпечка"],
            [Prep] = ["Варенье", "Консервы", "Маринады"],
            [SemiFinished] = ["Фарш", "Нарезка", "Заморозка"],
            [Sauces] = ["Горячие", "Холодные", "Заправки"]
        };

    /// <summary>Legacy alias — empty (no default Сырьё children).</summary>
    public static readonly string[] RawSubcategories = [];

    public static readonly string[] TemperaturePresets =
    [
        "+2…+6 °C",
        "0…+4 °C",
        "-18 °C",
        "комнатная"
    ];

    public static bool IsMarkingRootName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && Roots.Any(r => r.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the category is a marking root or a descendant of one.</summary>
    public static bool IsMarkingCategory(Category category, IReadOnlyList<Category> all)
    {
        var markingRootIds = all
            .Where(c => c.ParentId is null && IsMarkingRootName(c.Name))
            .Select(c => c.Id)
            .ToHashSet();

        if (markingRootIds.Contains(category.Id))
        {
            return true;
        }

        var byId = all.ToDictionary(c => c.Id);
        var current = category;
        while (current.ParentId is Guid parentId)
        {
            if (markingRootIds.Contains(parentId))
            {
                return true;
            }

            if (!byId.TryGetValue(parentId, out current!))
            {
                break;
            }
        }

        return false;
    }

    /// <summary>All marking root ids and their descendants.</summary>
    public static IReadOnlyList<Guid> GetAllMarkingCategoryIds(IReadOnlyList<Category> all)
    {
        var roots = all
            .Where(c => c.ParentId is null && IsMarkingRootName(c.Name))
            .Select(c => c.Id)
            .ToList();

        if (roots.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return GetSelfAndDescendantIds(all, roots);
    }

    /// <summary>Selected category plus all descendants (for list filters).</summary>
    public static IReadOnlyList<Guid> GetSelfAndDescendantIds(
        IReadOnlyList<Category> all,
        IEnumerable<Guid> rootIds)
    {
        var roots = rootIds.ToHashSet();
        if (roots.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var childrenByParent = all
            .Where(c => c.ParentId is not null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        var result = new HashSet<Guid>(roots);
        var queue = new Queue<Guid>(roots);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!childrenByParent.TryGetValue(id, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (result.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return result.ToList();
    }

    public static Guid? FindByName(IReadOnlyList<Category> all, string name, Guid? parentId = null)
    {
        return all.FirstOrDefault(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && c.ParentId == parentId)
            ?.Id;
    }
}
