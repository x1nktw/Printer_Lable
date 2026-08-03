using LabelPrint.Application.Marking;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint.Infrastructure.Persistence;

/// <summary>
/// Applies EF migrations with a pre-migration file backup and seeds defaults.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly LabelPrintDbContext _dbContext;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        LabelPrintDbContext dbContext,
        IOptions<DatabaseOptions> options,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Backs up an existing database file (if any), applies migrations, seeds defaults.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dbPath = _options.ResolveDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        if (File.Exists(dbPath))
        {
            await BackupDatabaseAsync(dbPath, cancellationToken);
        }

        _logger.LogInformation("Applying database migrations to {DatabasePath}", dbPath);
        await _dbContext.Database.MigrateAsync(cancellationToken);
        await SeedAsync(cancellationToken);
    }

    private Task BackupDatabaseAsync(string dbPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backupDir = ResolveBackupDirectory();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"labelprint_{stamp}.db.bak");
        File.Copy(dbPath, backupPath, overwrite: false);
        _logger.LogInformation("Created database backup {BackupPath}", backupPath);

        var backups = Directory.GetFiles(backupDir, "*.db.bak")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(_options.BackupRetentionCount)
            .ToList();

        foreach (var old in backups)
        {
            old.Delete();
            _logger.LogInformation("Deleted old database backup {BackupPath}", old.FullName);
        }

        return Task.CompletedTask;
    }

    private string ResolveBackupDirectory()
    {
        // Pre-migration: never materialize AppSettings entities — new columns may be missing.
        try
        {
            var backupPath = _dbContext.AppSettings
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => s.BackupPath)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(backupPath))
            {
                var configured = Environment.ExpandEnvironmentVariables(backupPath.Trim());
                Directory.CreateDirectory(configured);
                return configured;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read BackupPath before migrations; using default backup folder.");
        }

        return _options.ResolveBackupDirectory();
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Users.AnyAsync(cancellationToken))
        {
            _dbContext.Users.AddRange(
                new User { Name = "Администратор", Role = UserRole.Administrator, IsActive = true },
                new User { Name = "Оператор", Role = UserRole.Operator, IsActive = true });
        }

        if (!await _dbContext.AppSettings.AnyAsync(cancellationToken))
        {
            _dbContext.AppSettings.Add(new AppSettings());
        }
        else
        {
            var existing = await _dbContext.AppSettings
                .OrderBy(s => s.Id)
                .FirstAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(existing.FrontPadWebhookListenUrl))
            {
                existing.FrontPadWebhookListenUrl = "http://127.0.0.1:8765/";
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await EnsureSystemTemplatesAsync(cancellationToken);
        await EnsureRawMaterialsSeedAsync(cancellationToken);
        await EnsureAddonsSeedAsync(cancellationToken);
        await EnsureDefaultPrintTemplateSelectionsAsync(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDefaultPrintTemplateSelectionsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings.OrderBy(s => s.Id).FirstAsync(cancellationToken);
        if (settings.OrdersPrintTemplateId is not null && settings.MarkingPrintTemplateId is not null)
        {
            return;
        }

        var all = await _dbContext.LabelTemplates.AsNoTracking()
            .Where(t => !t.IsArchived)
            .ToListAsync(cancellationToken);
        if (all.Count == 0)
        {
            return;
        }

        var changed = false;
        if (settings.OrdersPrintTemplateId is null)
        {
            settings.OrdersPrintTemplateId =
                all.FirstOrDefault(t => t.Name.Contains("Кухня чек", StringComparison.OrdinalIgnoreCase))?.Id
                ?? all.FirstOrDefault(t => t.Name.Contains("Кухня", StringComparison.OrdinalIgnoreCase))?.Id
                ?? all[0].Id;
            changed = true;
        }

        if (settings.MarkingPrintTemplateId is null)
        {
            settings.MarkingPrintTemplateId =
                all.FirstOrDefault(t => t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))?.Id
                ?? all.FirstOrDefault(t => t.Name.Contains("Срок", StringComparison.OrdinalIgnoreCase))?.Id
                ?? all[0].Id;
            changed = true;
        }

        if (changed)
        {
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task EnsureAddonsSeedAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Addons.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.Addons.AddRange(
            new Addon
            {
                Name = "Халапеньо",
                MatchAliases = "халапень,перец,chili,jalap,острый",
                IconKey = "pepper"
            },
            new Addon
            {
                Name = "Сыр",
                MatchAliases = "cheese",
                IconKey = "cheese"
            },
            new Addon
            {
                Name = "Лук",
                MatchAliases = "onion",
                IconKey = "onion"
            });
    }

    private async Task EnsureSystemTemplatesAsync(CancellationToken cancellationToken)
    {
        var presets = new (string Name, double W, double H, string Json)[]
        {
            ("Ценник 58×40", 58, 40,
                """{"schemaVersion":1,"name":"Ценник 58x40","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":2,"width":54,"height":9},"bindingMode":1,"valueBinding":"ProductName","font":{"family":"Arial","sizePt":11,"bold":true}},{"id":"el2","type":0,"bounds":{"x":2,"y":12,"width":54,"height":10},"bindingMode":1,"valueBinding":"Price","font":{"family":"Arial","sizePt":16,"bold":true}},{"id":"el3","type":2,"bounds":{"x":4,"y":24,"width":50,"height":13},"symbology":0,"valueBinding":"Barcode"}]}"""),
            ("Срок годности 58×30", 58, 30,
                """{"schemaVersion":1,"name":"Срок годности","canvas":{"widthMm":58,"heightMm":30,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":2,"width":54,"height":8},"bindingMode":1,"valueBinding":"ProductName","font":{"family":"Arial","sizePt":11,"bold":true}},{"id":"el2","type":0,"bounds":{"x":2,"y":12,"width":54,"height":7},"bindingMode":1,"valueBinding":"Date","font":{"family":"Arial","sizePt":9}},{"id":"el3","type":0,"bounds":{"x":2,"y":20,"width":54,"height":7},"bindingMode":1,"valueBinding":"ExpireDate","font":{"family":"Arial","sizePt":10,"bold":true}}]}"""),
            ("Позиция заказа 58×40", 58, 40,
                """{"schemaVersion":1,"name":"Позиция заказа","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":2,"width":54,"height":7},"bindingMode":1,"valueBinding":"OrderNumber","font":{"family":"Arial","sizePt":10}},{"id":"el2","type":0,"bounds":{"x":2,"y":11,"width":54,"height":14},"bindingMode":1,"valueBinding":"PositionName","font":{"family":"Arial","sizePt":14,"bold":true}},{"id":"el3","type":0,"bounds":{"x":2,"y":28,"width":54,"height":8},"content":"{{PositionIndex}}/{{PositionTotal}}","bindingMode":0,"font":{"family":"Arial","sizePt":12}}]}"""),
            ("Сырьё 58×40", 58, 40,
                """{"schemaVersion":1,"name":"Сырьё 58x40","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":1,"width":54,"height":12},"bindingMode":1,"valueBinding":"ProductName","font":{"family":"Arial","sizePt":14,"bold":true}},{"id":"el2","type":0,"bounds":{"x":2,"y":14,"width":28,"height":7},"bindingMode":1,"valueBinding":"Date","font":{"family":"Arial","sizePt":9}},{"id":"el3","type":0,"bounds":{"x":30,"y":14,"width":26,"height":7},"bindingMode":1,"valueBinding":"Time","font":{"family":"Arial","sizePt":9,"bold":true}},{"id":"el4","type":0,"bounds":{"x":2,"y":22,"width":54,"height":7},"bindingMode":1,"valueBinding":"TemperatureRegime","font":{"family":"Arial","sizePt":9}},{"id":"el5","type":0,"bounds":{"x":2,"y":30,"width":54,"height":8},"bindingMode":1,"valueBinding":"ExpireDate","font":{"family":"Arial","sizePt":11,"bold":true}}]}"""),
            ("Штрихкод 58×40", 58, 40,
                """{"schemaVersion":1,"name":"Штрихкод 58x40","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":2,"width":54,"height":7},"bindingMode":1,"valueBinding":"ProductName","font":{"family":"Arial","sizePt":9}},{"id":"el2","type":2,"bounds":{"x":3,"y":10,"width":52,"height":20},"symbology":0,"valueBinding":"Barcode"},{"id":"el3","type":0,"bounds":{"x":2,"y":31,"width":54,"height":6},"bindingMode":1,"valueBinding":"Sku","font":{"family":"Arial","sizePt":9}}]}"""),
            ("Кухня 58×40", 58, 40,
                """{"schemaVersion":1,"name":"Кухня 58x40","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[{"id":"el1","type":0,"bounds":{"x":2,"y":2,"width":36,"height":8},"content":"Заказ {{OrderNumber}}","bindingMode":0,"font":{"family":"Inter","sizePt":10,"bold":true}},{"id":"el2","type":0,"bounds":{"x":38,"y":2,"width":18,"height":8},"content":"{{PositionIndex}}/{{PositionTotal}}","bindingMode":0,"font":{"family":"Inter","sizePt":10}},{"id":"el3","type":0,"bounds":{"x":2,"y":14,"width":54,"height":20},"bindingMode":1,"valueBinding":"PositionName","font":{"family":"Inter","sizePt":15,"bold":true}}]}"""),
            ("Кухня чек 40×58", 40, 58,
                """{"schemaVersion":1,"name":"Кухня чек 40x58","canvas":{"widthMm":40,"heightMm":58,"dpi":203},"elements":[{"id":"hdr","type":4,"bounds":{"x":0,"y":0,"width":40,"height":12},"filled":true,"z":0},{"id":"lbl","type":0,"bounds":{"x":1.5,"y":0.7,"width":18,"height":3},"content":"Заказ:","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"num","type":0,"bounds":{"x":1.5,"y":3.4,"width":18,"height":8},"bindingMode":1,"valueBinding":"OrderNumber","invert":true,"font":{"family":"Inter","sizePt":16,"bold":true},"z":1},{"id":"vdiv","type":6,"bounds":{"x":20.5,"y":1.5,"width":0,"height":9},"invert":true,"dashed":true,"strokeThickness":0.22,"z":1},{"id":"ical","type":1,"bounds":{"x":22,"y":1.8,"width":3.2,"height":3.2},"imagePath":"asset:icons/calendar-white.png","z":1},{"id":"date","type":0,"bounds":{"x":25.8,"y":1.9,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Date","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"iclk","type":1,"bounds":{"x":22,"y":6.6,"width":3.2,"height":3.2},"imagePath":"asset:icons/clock-white.png","z":1},{"id":"time","type":0,"bounds":{"x":25.8,"y":6.7,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Time","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"hdiv","type":6,"bounds":{"x":0,"y":12,"width":40,"height":0},"dashed":true,"strokeThickness":0.28,"z":2},{"id":"name","type":0,"bounds":{"x":1.5,"y":13.2,"width":37,"height":13},"bindingMode":1,"valueBinding":"PositionName","font":{"family":"Inter","sizePt":14,"bold":true},"z":2},{"id":"addons","type":0,"bounds":{"x":1.5,"y":27,"width":37,"height":22},"bindingMode":1,"valueBinding":"AddonsKitchen","font":{"family":"Inter","sizePt":8,"bold":true},"z":2},{"id":"badge","type":4,"bounds":{"x":27.5,"y":51.5,"width":11,"height":5},"filled":true,"cornerRadiusMm":1.2,"z":3},{"id":"idx","type":0,"bounds":{"x":28,"y":52.1,"width":10,"height":4},"content":"{{PositionIndex}}/{{PositionTotal}}","invert":true,"font":{"family":"Inter","sizePt":9,"bold":true},"z":4}]}""")
        };

        foreach (var (name, w, h, json) in presets)
        {
            var existing = await _dbContext.LabelTemplates
                .FirstOrDefaultAsync(t => t.IsSystemPreset && t.Name == name, cancellationToken);
            if (existing is null)
            {
                _dbContext.LabelTemplates.Add(CreatePreset(name, w, h, json));
            }
            else
            {
                existing.WidthMm = w;
                existing.HeightMm = h;
                existing.ContentJson = json;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await ArchiveLegacyKitchenChecksAsync(presets, cancellationToken);
    }

    private async Task ArchiveLegacyKitchenChecksAsync(
        (string Name, double W, double H, string Json)[] presets,
        CancellationToken cancellationToken)
    {
        foreach (var legacyName in new[] { "Кухня чек 58×80", "Кухня чек 58×40" })
        {
            var legacy = await _dbContext.LabelTemplates
                .FirstOrDefaultAsync(t => t.IsSystemPreset && t.Name == legacyName && !t.IsArchived, cancellationToken);
            if (legacy is null)
            {
                continue;
            }

            var hasNew = await _dbContext.LabelTemplates
                .AnyAsync(t => t.IsSystemPreset && t.Name == "Кухня чек 40×58", cancellationToken);
            if (hasNew)
            {
                legacy.IsArchived = true;
                legacy.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var check = presets.First(p => p.Name == "Кухня чек 40×58");
                legacy.Name = check.Name;
                legacy.WidthMm = check.W;
                legacy.HeightMm = check.H;
                legacy.ContentJson = check.Json;
                legacy.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private async Task EnsureRawMaterialsSeedAsync(CancellationToken cancellationToken)
    {
        var rawRoot = await EnsureMarkingCategoryAsync(MarkingCategories.Raw, parentId: null, sortOrder: 0, cancellationToken);
        var prepRoot = await EnsureMarkingCategoryAsync(MarkingCategories.Prep, parentId: null, sortOrder: 1, cancellationToken);
        var semiRoot = await EnsureMarkingCategoryAsync(MarkingCategories.SemiFinished, parentId: null, sortOrder: 2, cancellationToken);
        var sauceRoot = await EnsureMarkingCategoryAsync(MarkingCategories.Sauces, parentId: null, sortOrder: 3, cancellationToken);

        var rootByName = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            [MarkingCategories.Raw] = rawRoot,
            [MarkingCategories.Prep] = prepRoot,
            [MarkingCategories.SemiFinished] = semiRoot,
            [MarkingCategories.Sauces] = sauceRoot
        };

        var subIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rootName, children) in MarkingCategories.DefaultSubcategories)
        {
            if (!rootByName.TryGetValue(rootName, out var root))
            {
                continue;
            }

            for (var i = 0; i < children.Length; i++)
            {
                var name = children[i];
                var sub = await EnsureMarkingCategoryAsync(name, root.Id, sortOrder: i, cancellationToken);
                // Prefer Сырьё children for sample product mapping keys.
                if (rootName.Equals(MarkingCategories.Raw, StringComparison.OrdinalIgnoreCase)
                    || !subIds.ContainsKey(name))
                {
                    subIds[name] = sub.Id;
                }
            }
        }

        var rawTemplate = await _dbContext.LabelTemplates
            .FirstOrDefaultAsync(t => t.IsSystemPreset && t.Name == "Сырьё 58×40" && !t.IsArchived, cancellationToken);

        var samples = new (string Name, string Sku, string Subcategory, string? Temperature)[]
        {
            ("Мясо", "RAW-MEAT", "Мясо", "+2…+6 °C"),
            ("Курица", "RAW-CHICKEN", "Мясо", "+2…+6 °C"),
            ("Рыба", "RAW-FISH", "Мясо", "0…+4 °C"),
            ("Томаты", "RAW-TOMATO", "Овощи", "+2…+6 °C"),
            ("Лук", "RAW-ONION", "Овощи", "комнатная"),
            ("Огурцы", "RAW-CUCUMBER", "Овощи", "+2…+6 °C"),
            ("Сыр", "RAW-CHEESE", "Сыр", "+2…+6 °C")
        };

        foreach (var (name, sku, subcategory, temperature) in samples)
        {
            if (await _dbContext.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
            {
                continue;
            }

            var categoryId = subIds.TryGetValue(subcategory, out var id) ? id : rawRoot.Id;
            _dbContext.Products.Add(new Product
            {
                Name = name,
                Sku = sku,
                PriceAmount = 0,
                PriceCurrency = "RUB",
                CategoryId = categoryId,
                TemperatureRegime = temperature,
                DefaultTemplateId = rawTemplate?.Id
            });
        }
    }

    private async Task<Category> EnsureMarkingCategoryAsync(
        string name,
        Guid? parentId,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Categories
            .FirstOrDefaultAsync(
                c => c.Name == name && c.ParentId == parentId && !c.IsArchived,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.SortOrder != sortOrder)
            {
                existing.SortOrder = sortOrder;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }

            return existing;
        }

        var category = new Category
        {
            Name = name,
            ParentId = parentId,
            SortOrder = sortOrder
        };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return category;
    }

    private static LabelTemplate CreatePreset(string name, double width, double height, string json) => new()
    {
        Name = name,
        WidthMm = width,
        HeightMm = height,
        SchemaVersion = 1,
        ContentJson = json,
        IsSystemPreset = true,
        Description = "Системная заготовка"
    };
}
