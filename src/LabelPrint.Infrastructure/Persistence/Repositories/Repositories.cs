using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LabelPrint.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository : ICategoryRepository
{
    private readonly LabelPrintDbContext _db;

    public CategoryRepository(LabelPrintDbContext db) => _db = db;

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _db.Categories.AddAsync(category, cancellationToken);

    public void Update(Category category) => _db.Categories.Update(category);

    public async Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is not null)
        {
            category.IsArchived = true;
            category.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}

internal sealed class CustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
{
    private readonly LabelPrintDbContext _db;

    public CustomFieldDefinitionRepository(LabelPrintDbContext db) => _db = db;

    public Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.CustomFieldDefinitions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.CustomFieldDefinitions.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default) =>
        await _db.CustomFieldDefinitions.AddAsync(definition, cancellationToken);

    public void Update(CustomFieldDefinition definition) => _db.CustomFieldDefinitions.Update(definition);
}

internal sealed class TemplateRepository : ITemplateRepository
{
    private readonly LabelPrintDbContext _db;

    public TemplateRepository(LabelPrintDbContext db) => _db = db;

    public Task<LabelTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.LabelTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<LabelTemplate> Items, int TotalCount)> SearchAsync(
        string? search,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _db.LabelTemplates.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(t => !t.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => EF.Functions.Like(t.Name, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(t => t.Name).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(LabelTemplate template, CancellationToken cancellationToken = default) =>
        await _db.LabelTemplates.AddAsync(template, cancellationToken);

    public void Update(LabelTemplate template) => _db.LabelTemplates.Update(template);

    public async Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.LabelTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is not null)
        {
            template.IsArchived = true;
            template.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}

internal sealed class PrintJobRepository : IPrintJobRepository
{
    private readonly LabelPrintDbContext _db;

    public PrintJobRepository(LabelPrintDbContext db) => _db = db;

    public Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PrintJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<PrintJob?> TryClaimNextAsync(Guid printerId, Guid expectedRowVersion, CancellationToken cancellationToken = default)
    {
        var job = await _db.PrintJobs
            .Where(j => j.PrinterId == printerId && j.Status == PrintJobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return null;
        }

        // Optimistic concurrency: caller may pass the previously observed RowVersion.
        if (expectedRowVersion != Guid.Empty && job.RowVersion != expectedRowVersion)
        {
            return null;
        }

        return job;
    }

    public async Task<IReadOnlyList<PrintJob>> GetByStatusAsync(PrintJobStatus status, CancellationToken cancellationToken = default) =>
        await _db.PrintJobs.AsNoTracking().Where(j => j.Status == status).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PrintJob>> ListQueueAsync(CancellationToken cancellationToken = default)
    {
        var active = new[]
        {
            PrintJobStatus.Pending,
            PrintJobStatus.Rendering,
            PrintJobStatus.Printing,
            PrintJobStatus.Failed
        };

        return await _db.PrintJobs
            .AsNoTracking()
            .Include(j => j.Printer)
            .Where(j => active.Contains(j.Status))
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PrintJob job, CancellationToken cancellationToken = default) =>
        await _db.PrintJobs.AddAsync(job, cancellationToken);

    public void Update(PrintJob job) => _db.PrintJobs.Update(job);
}

internal sealed class PrinterRepository : IPrinterRepository
{
    private readonly LabelPrintDbContext _db;

    public PrinterRepository(LabelPrintDbContext db) => _db = db;

    public Task<Printer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Printers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        _db.Printers
            .Where(p => p.IsDefault && p.IsActive)
            .OrderBy(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Printer>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Printers.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Printer printer, CancellationToken cancellationToken = default) =>
        await _db.Printers.AddAsync(printer, cancellationToken);

    public void Update(Printer printer) => _db.Printers.Update(printer);

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var printer = await _db.Printers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (printer is not null)
        {
            printer.IsActive = false;
            printer.IsDefault = false;
            printer.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public async Task ClearDefaultFlagAsync(CancellationToken cancellationToken = default)
    {
        var defaults = await _db.Printers.Where(p => p.IsDefault).ToListAsync(cancellationToken);
        foreach (var printer in defaults)
        {
            printer.IsDefault = false;
            printer.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}

internal sealed class PrintHistoryRepository : IPrintHistoryRepository
{
    private readonly LabelPrintDbContext _db;

    public PrintHistoryRepository(LabelPrintDbContext db) => _db = db;

    public Task<PrintHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PrintHistory.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task AddAsync(PrintHistory entry, CancellationToken cancellationToken = default) =>
        await _db.PrintHistory.AddAsync(entry, cancellationToken);

    public async Task<CursorPage<PrintHistory>> GetPageAsync(
        DateTimeOffset? before,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.PrintHistory.AsNoTracking().AsQueryable();
        if (before is not null)
        {
            query = query.Where(h => h.PrintedAt < before);
        }

        var items = await query
            .OrderByDescending(h => h.PrintedAt)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore && items.Count > 0
            ? items[^1].PrintedAt.UtcTicks.ToString()
            : null;

        return new CursorPage<PrintHistory>(items, nextCursor, hasMore);
    }
}

internal sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly LabelPrintDbContext _db;

    public AppSettingsRepository(LabelPrintDbContext db) => _db = db;

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.AppSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettings();
        await _db.AppSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public void Update(AppSettings settings) => _db.AppSettings.Update(settings);
}

internal sealed class UserRepository : IUserRepository
{
    private readonly LabelPrintDbContext _db;

    public UserRepository(LabelPrintDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default) =>
        await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
}

internal sealed class AddonRepository : IAddonRepository
{
    private readonly LabelPrintDbContext _db;

    public AddonRepository(LabelPrintDbContext db) => _db = db;

    public async Task<IReadOnlyList<Addon>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Addons.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(a => !a.IsArchived);
        }

        return await query.OrderBy(a => a.Name).ToListAsync(cancellationToken);
    }

    public Task<Addon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Addons.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Addon addon, CancellationToken cancellationToken = default) =>
        await _db.Addons.AddAsync(addon, cancellationToken);

    public void Update(Addon addon) => _db.Addons.Update(addon);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly LabelPrintDbContext _db;

    public UnitOfWork(LabelPrintDbContext db)
    {
        _db = db;
        Products = new ProductRepository(db);
        Categories = new CategoryRepository(db);
        CustomFieldDefinitions = new CustomFieldDefinitionRepository(db);
        Templates = new TemplateRepository(db);
        PrintJobs = new PrintJobRepository(db);
        PrintHistory = new PrintHistoryRepository(db);
        Printers = new PrinterRepository(db);
        Settings = new AppSettingsRepository(db);
        Users = new UserRepository(db);
        Orders = new OrderRepository(db);
        Addons = new AddonRepository(db);
    }

    public IProductRepository Products { get; }

    public ICategoryRepository Categories { get; }

    public ICustomFieldDefinitionRepository CustomFieldDefinitions { get; }

    public ITemplateRepository Templates { get; }

    public IPrintJobRepository PrintJobs { get; }

    public IPrintHistoryRepository PrintHistory { get; }

    public IPrinterRepository Printers { get; }

    public IAppSettingsRepository Settings { get; }

    public IUserRepository Users { get; }

    public IOrderRepository Orders { get; }

    public IAddonRepository Addons { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
