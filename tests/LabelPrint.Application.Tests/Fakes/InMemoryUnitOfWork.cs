using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Tests.Fakes;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly InMemoryUserRepository _users = new();

    public InMemoryUnitOfWork()
    {
        Products = new InMemoryProductRepository(this);
        Categories = new InMemoryCategoryRepository();
        CustomFieldDefinitions = new InMemoryCustomFieldDefinitionRepository();
        Templates = new InMemoryTemplateRepository();
        PrintJobs = new InMemoryPrintJobRepository();
        PrintHistory = new InMemoryPrintHistoryRepository();
        Printers = new InMemoryPrinterRepository();
        Settings = new InMemoryAppSettingsRepository();
        Users = _users;
        Orders = new InMemoryOrderRepository();
        Addons = new InMemoryAddonRepository();
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

    public void AddUser(User user) => _users.Add(user);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly InMemoryUnitOfWork _uow;
        private readonly List<Product> _items = new();

        public InMemoryProductRepository(InMemoryUnitOfWork uow) => _uow = uow;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Id == id));

        public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Barcode == barcode));

        public Task<Product?> GetByNameExactAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !p.IsArchived));

        public Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase) && p.Id != excludeId));

        public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(p => p.Barcode == barcode && p.Id != excludeId));

        public Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
            string? search,
            Guid? categoryId,
            bool includeArchived,
            int skip,
            int take,
            CancellationToken cancellationToken = default,
            Guid? excludeCategoryId = null)
        {
            IEnumerable<Product> query = _items;
            if (!includeArchived)
            {
                query = query.Where(p => !p.IsArchived);
            }

            if (categoryId is not null)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }
            else if (excludeCategoryId is not null)
            {
                query = query.Where(p => p.CategoryId != excludeCategoryId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (p.Barcode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var list = query.OrderBy(p => p.Name).ToList();
            foreach (var product in list)
            {
                if (product.CategoryId is Guid catId)
                {
                    product.Category = _uow.Categories.GetByIdAsync(catId).GetAwaiter().GetResult();
                }
            }

            return Task.FromResult(((IReadOnlyList<Product>)list.Skip(skip).Take(take).ToList(), list.Count));
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _items.Add(product);
            return Task.CompletedTask;
        }

        public void Update(Product product)
        {
            var index = _items.FindIndex(p => p.Id == product.Id);
            if (index >= 0)
            {
                _items[index] = product;
            }
        }

        public Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var product = _items.FirstOrDefault(p => p.Id == id);
            if (product is not null)
            {
                product.IsArchived = true;
                product.UpdatedAt = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly List<Category> _items = new();

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<Category>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            IEnumerable<Category> query = _items;
            if (!includeArchived)
            {
                query = query.Where(c => !c.IsArchived);
            }

            return Task.FromResult((IReadOnlyList<Category>)query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList());
        }

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            _items.Add(category);
            return Task.CompletedTask;
        }

        public void Update(Category category)
        {
            var index = _items.FindIndex(c => c.Id == category.Id);
            if (index >= 0)
            {
                _items[index] = category;
            }
        }

        public Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = _items.FirstOrDefault(c => c.Id == id);
            if (category is not null)
            {
                category.IsArchived = true;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
    {
        private readonly List<CustomFieldDefinition> _items = new();

        public Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            IEnumerable<CustomFieldDefinition> query = _items;
            if (!includeArchived)
            {
                query = query.Where(c => !c.IsArchived);
            }

            return Task.FromResult((IReadOnlyList<CustomFieldDefinition>)query.ToList());
        }

        public Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
        {
            _items.Add(definition);
            return Task.CompletedTask;
        }

        public void Update(CustomFieldDefinition definition)
        {
            var index = _items.FindIndex(c => c.Id == definition.Id);
            if (index >= 0)
            {
                _items[index] = definition;
            }
        }
    }

    private sealed class InMemoryTemplateRepository : ITemplateRepository
    {
        private readonly List<LabelTemplate> _items = new();

        public Task<LabelTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(t => t.Id == id));

        public Task<(IReadOnlyList<LabelTemplate> Items, int TotalCount)> SearchAsync(
            string? search,
            bool includeArchived,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<LabelTemplate> query = _items;
            if (!includeArchived)
            {
                query = query.Where(t => !t.IsArchived);
            }

            var list = query.ToList();
            return Task.FromResult(((IReadOnlyList<LabelTemplate>)list.Skip(skip).Take(take).ToList(), list.Count));
        }

        public Task AddAsync(LabelTemplate template, CancellationToken cancellationToken = default)
        {
            _items.Add(template);
            return Task.CompletedTask;
        }

        public void Update(LabelTemplate template)
        {
            var index = _items.FindIndex(t => t.Id == template.Id);
            if (index >= 0)
            {
                _items[index] = template;
            }
        }

        public Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var template = _items.FirstOrDefault(t => t.Id == id);
            if (template is not null)
            {
                template.IsArchived = true;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPrintJobRepository : IPrintJobRepository
    {
        private readonly List<PrintJob> _items = new();

        public Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(j => j.Id == id));

        public Task<PrintJob?> TryClaimNextAsync(Guid printerId, Guid expectedRowVersion, CancellationToken cancellationToken = default)
        {
            var job = _items
                .Where(j => j.PrinterId == printerId && j.Status == PrintJobStatus.Pending)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefault();

            if (job is null)
            {
                return Task.FromResult<PrintJob?>(null);
            }

            if (expectedRowVersion != Guid.Empty && job.RowVersion != expectedRowVersion)
            {
                return Task.FromResult<PrintJob?>(null);
            }

            return Task.FromResult<PrintJob?>(job);
        }

        public Task<IReadOnlyList<PrintJob>> GetByStatusAsync(PrintJobStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<PrintJob>)_items.Where(j => j.Status == status).ToList());

        public Task<IReadOnlyList<PrintJob>> ListQueueAsync(CancellationToken cancellationToken = default)
        {
            var active = new[]
            {
                PrintJobStatus.Pending,
                PrintJobStatus.Rendering,
                PrintJobStatus.Printing,
                PrintJobStatus.Failed
            };

            return Task.FromResult((IReadOnlyList<PrintJob>)_items
                .Where(j => active.Contains(j.Status))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .ToList());
        }

        public Task AddAsync(PrintJob job, CancellationToken cancellationToken = default)
        {
            _items.Add(job);
            return Task.CompletedTask;
        }

        public void Update(PrintJob job)
        {
            var index = _items.FindIndex(j => j.Id == job.Id);
            if (index >= 0)
            {
                _items[index] = job;
            }
        }

        public IReadOnlyList<PrintJob> All => _items;
    }

    private sealed class InMemoryPrinterRepository : IPrinterRepository
    {
        private readonly List<Printer> _items = new();

        public Task<Printer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Id == id));

        public Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.IsDefault && p.IsActive));

        public Task<IReadOnlyList<Printer>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            IEnumerable<Printer> query = _items;
            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            return Task.FromResult((IReadOnlyList<Printer>)query.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToList());
        }

        public Task AddAsync(Printer printer, CancellationToken cancellationToken = default)
        {
            _items.Add(printer);
            return Task.CompletedTask;
        }

        public void Update(Printer printer)
        {
            var index = _items.FindIndex(p => p.Id == printer.Id);
            if (index >= 0)
            {
                _items[index] = printer;
            }
        }

        public Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var printer = _items.FirstOrDefault(p => p.Id == id);
            if (printer is not null)
            {
                printer.IsActive = false;
                printer.IsDefault = false;
            }

            return Task.CompletedTask;
        }

        public Task ClearDefaultFlagAsync(CancellationToken cancellationToken = default)
        {
            foreach (var printer in _items.Where(p => p.IsDefault))
            {
                printer.IsDefault = false;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPrintHistoryRepository : IPrintHistoryRepository
    {
        private readonly List<PrintHistory> _items = new();

        public Task<PrintHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(h => h.Id == id));

        public Task AddAsync(PrintHistory entry, CancellationToken cancellationToken = default)
        {
            _items.Add(entry);
            return Task.CompletedTask;
        }

        public Task<CursorPage<PrintHistory>> GetPageAsync(
            DateTimeOffset? before,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<PrintHistory> query = _items;
            if (before is not null)
            {
                query = query.Where(h => h.PrintedAt < before);
            }

            var list = query.OrderByDescending(h => h.PrintedAt).Take(pageSize + 1).ToList();
            var hasMore = list.Count > pageSize;
            if (hasMore)
            {
                list.RemoveAt(list.Count - 1);
            }

            var nextCursor = hasMore && list.Count > 0
                ? list[^1].PrintedAt.UtcTicks.ToString()
                : null;

            return Task.FromResult(new CursorPage<PrintHistory>(list, nextCursor, hasMore));
        }
    }

    private sealed class InMemoryAppSettingsRepository : IAppSettingsRepository
    {
        private AppSettings _settings = new();

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public void Update(AppSettings settings) => _settings = settings;
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _items = new();

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<User>)_items.Where(u => u.IsActive).OrderBy(u => u.Name).ToList());

        public void Add(User user) => _items.Add(user);
    }

    private sealed class InMemoryOrderRepository : IOrderRepository
    {
        private readonly List<Order> _items = new();

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(o => o.Id == id));

        public Task<Order?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(o =>
                o.ExternalOrderId.Equals(externalOrderId, StringComparison.OrdinalIgnoreCase)));

        public Task<OrderItem?> GetItemByIdAsync(Guid orderItemId, CancellationToken cancellationToken = default)
        {
            foreach (var order in _items)
            {
                var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
                if (item is not null)
                {
                    item.Order = order;
                    return Task.FromResult<OrderItem?>(item);
                }
            }

            return Task.FromResult<OrderItem?>(null);
        }

        public Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
            string? search,
            OrderStatus? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Order> query = _items;
            if (status is OrderStatus s)
            {
                query = query.Where(o => o.Status == s);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(o =>
                    o.Number.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    o.ExternalOrderId.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var list = query.OrderByDescending(o => o.OrderedAt ?? o.ReceivedAt).ToList();
            return Task.FromResult(((IReadOnlyList<Order>)list.Skip(skip).Take(take).ToList(), list.Count));
        }

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            _items.Add(order);
            return Task.CompletedTask;
        }

        public void Update(Order order)
        {
            var index = _items.FindIndex(o => o.Id == order.Id);
            if (index >= 0)
            {
                _items[index] = order;
            }
        }
    }

    private sealed class InMemoryAddonRepository : IAddonRepository
    {
        private readonly List<Addon> _items = new();

        public Task<IReadOnlyList<Addon>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            IEnumerable<Addon> query = _items;
            if (!includeArchived)
            {
                query = query.Where(a => !a.IsArchived);
            }

            return Task.FromResult((IReadOnlyList<Addon>)query.OrderBy(a => a.Name).ToList());
        }

        public Task<Addon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(a => a.Id == id));

        public Task AddAsync(Addon addon, CancellationToken cancellationToken = default)
        {
            _items.Add(addon);
            return Task.CompletedTask;
        }

        public void Update(Addon addon)
        {
            var index = _items.FindIndex(a => a.Id == addon.Id);
            if (index >= 0)
            {
                _items[index] = addon;
            }
        }
    }
}
