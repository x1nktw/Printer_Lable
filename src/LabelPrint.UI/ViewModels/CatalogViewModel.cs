using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Text;

namespace LabelPrint.UI.ViewModels;

public partial class CatalogViewModel : PageViewModelBase
{
    private const int PageSize = 100;
    private readonly IServiceScopeFactory _scopeFactory;
    private int _loadedCount;
    private int _totalCount;

    public CatalogViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "Каталог";
    }

    public ObservableCollection<ProductListItemDto> Products { get; } = new();
    public ObservableCollection<CategoryNodeVm> Categories { get; } = new();
    public ObservableCollection<CategoryOptionVm> CategoryOptions { get; } = new();
    public ObservableCollection<CustomFieldEditVm> CustomFields { get; } = new();
    public ObservableCollection<CustomFieldDefinitionDto> FieldDefinitions { get; } = new();
    public ObservableCollection<PrinterListItemDto> Printers { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private ProductListItemDto? _selectedProduct;
    [ObservableProperty] private CategoryNodeVm? _selectedCategory;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canLoadMore;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editSku = string.Empty;
    [ObservableProperty] private string? _editBarcode;
    [ObservableProperty] private decimal? _editPrice;
    [ObservableProperty] private Guid? _editCategoryId;
    [ObservableProperty] private CategoryOptionVm? _editCategoryOption;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private Guid? _editingId;
    [ObservableProperty] private string _newCategoryName = string.Empty;
    [ObservableProperty] private string _newFieldName = string.Empty;
    [ObservableProperty] private bool _newFieldRequired;
    [ObservableProperty] private bool _showFieldManager;
    [ObservableProperty] private CustomFieldDefinitionDto? _selectedFieldDefinition;
    [ObservableProperty] private PrinterListItemDto? _selectedPrinter;
    [ObservableProperty] private bool _useCustomLabelDateTime;
    [ObservableProperty] private DateTime? _labelDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? _labelTime = DateTime.Now.TimeOfDay;

    partial void OnSelectedCategoryChanged(CategoryNodeVm? value) => _ = ReloadProductsAsync();

    partial void OnEditCategoryOptionChanged(CategoryOptionVm? value) => EditCategoryId = value?.Id;

    [RelayCommand]
    private async Task LoadAsync() => await ReloadAllAsync();

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !CanLoadMore)
        {
            return;
        }

        await LoadProductsPageAsync(append: true);
    }

    [RelayCommand]
    private async Task NewProductAsync()
    {
        EditingId = null;
        EditName = string.Empty;
        EditSku = string.Empty;
        EditBarcode = null;
        EditPrice = 0;
        EditCategoryId = SelectedCategory?.Id;
        EditCategoryOption = CategoryOptions.FirstOrDefault(c => c.Id == EditCategoryId);
        await LoadCustomFieldEditorsAsync(null);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var result = await products.GetAsync(SelectedProduct.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dto = result.Value;
        EditingId = SelectedProduct.Id;
        EditName = dto.Name;
        EditSku = dto.Sku;
        EditBarcode = dto.Barcode;
        EditPrice = dto.PriceAmount;
        EditCategoryId = dto.CategoryId;
        EditCategoryOption = CategoryOptions.FirstOrDefault(c => c.Id == EditCategoryId);
        await LoadCustomFieldEditorsAsync(dto.CustomFieldValues);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var dto = new ProductUpsertDto
        {
            Name = EditName,
            Sku = EditSku,
            Barcode = EditBarcode,
            PriceAmount = EditPrice ?? 0,
            CategoryId = EditCategoryId,
            CustomFieldValues = CustomFields.ToDictionary(f => f.DefinitionId, f => f.Value)
        };

        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();

        if (EditingId is Guid id)
        {
            var update = await products.UpdateAsync(id, dto);
            if (update.IsFailure)
            {
                StatusMessage = update.Error;
                return;
            }
        }
        else
        {
            var create = await products.CreateAsync(dto);
            if (create.IsFailure)
            {
                StatusMessage = create.Error;
                return;
            }
        }

        IsEditorOpen = false;
        StatusMessage = "Сохранено";
        await ReloadProductsAsync();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var result = await products.ArchiveAsync(SelectedProduct.Id);
        StatusMessage = result.IsFailure ? result.Error : "Товар архивирован";
        await ReloadProductsAsync();
    }

    [RelayCommand]
    private async Task SyncFrontPadCatalogAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<IFrontPadCatalogSyncService>();
        var result = await sync.SyncProductsAsync();
        StatusMessage = result.IsFailure ? result.Error : result.Value.Message;
        if (result.IsSuccess)
        {
            await ReloadProductsAsync();
        }
    }

    [RelayCommand]
    private async Task PrintSelectedAsync()
    {
        if (SelectedProduct is null)
        {
            StatusMessage = "Выберите товар для печати.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var printService = scope.ServiceProvider.GetRequiredService<IPrintService>();
        var printerId = SelectedPrinter?.Id;
        DateTimeOffset? overrideDt = null;
        if (UseCustomLabelDateTime)
        {
            var date = (LabelDate ?? DateTime.Today).Date;
            var time = LabelTime ?? TimeSpan.Zero;
            overrideDt = new DateTimeOffset(date.Add(time));
        }

        var result = await printService.PrintProductAsync(
            SelectedProduct.Id,
            printerId,
            copies: 1,
            labelDateTimeOverride: overrideDt);
        if (result.IsFailure)
        {
            StatusMessage = result.Error?.Contains("No active printer", StringComparison.OrdinalIgnoreCase) == true
                ? "Нет принтера. Добавьте виртуальный принтер в разделе «Принтеры»."
                : result.Error;
            return;
        }

        StatusMessage = $"Задание {result.Value} добавлено в очередь печати.";
    }

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusMessage = "Укажите название категории.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var result = await categories.CreateAsync(NewCategoryName, SelectedCategory?.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        NewCategoryName = string.Empty;
        StatusMessage = "Категория создана";
        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task ArchiveCategoryAsync()
    {
        if (SelectedCategory is null || SelectedCategory.Id is null)
        {
            StatusMessage = "Выберите категорию.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var result = await categories.ArchiveAsync(SelectedCategory.Id.Value);
        StatusMessage = result.IsFailure ? result.Error : "Категория в архиве";
        SelectedCategory = Categories.FirstOrDefault();
        await LoadCategoriesAsync();
        await ReloadProductsAsync();
    }

    [RelayCommand]
    private async Task ExportLabelPngAsync()
    {
        if (SelectedProduct is null)
        {
            StatusMessage = "Выберите товар для экспорта.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var export = scope.ServiceProvider.GetRequiredService<IExportService>();
        var result = await export.RenderProductLabelPngAsync(SelectedProduct.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var path = WriteExportFile($"label_{SelectedProduct.Sku}", ".png", result.Value);
        StatusMessage = $"PNG: {path}";
    }

    [RelayCommand]
    private async Task ExportLabelPdfAsync()
    {
        if (SelectedProduct is null)
        {
            StatusMessage = "Выберите товар для экспорта.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var export = scope.ServiceProvider.GetRequiredService<IExportService>();
        var result = await export.RenderProductLabelPdfAsync(SelectedProduct.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var path = WriteExportFile($"label_{SelectedProduct.Sku}", ".pdf", result.Value);
        StatusMessage = $"PDF: {path}";
    }

    private static string WriteExportFile(string prefix, string extension, byte[] content)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "exports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var csv = scope.ServiceProvider.GetRequiredService<IProductCsvService>();
        var result = await csv.ExportAsync();
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "exports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"products_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await File.WriteAllTextAsync(path, result.Value, Encoding.UTF8);
        StatusMessage = $"Экспорт: {path}";
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "exports");
        Directory.CreateDirectory(dir);
        var latest = Directory.GetFiles(dir, "*.csv")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        if (latest is null)
        {
            StatusMessage = $"Нет CSV в {dir}. Положите файл туда и повторите импорт.";
            return;
        }

        var text = await File.ReadAllTextAsync(latest.FullName, Encoding.UTF8);
        using var scope = _scopeFactory.CreateScope();
        var csv = scope.ServiceProvider.GetRequiredService<IProductCsvService>();
        var result = await csv.ImportAsync(text);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        StatusMessage = $"Импортировано из {latest.Name}: {result.Value} строк";
        await ReloadProductsAsync();
    }

    [RelayCommand]
    private async Task CreateCustomFieldAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFieldName))
        {
            StatusMessage = "Укажите имя поля.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var fields = scope.ServiceProvider.GetRequiredService<ICustomFieldService>();
        var result = await fields.CreateAsync(NewFieldName, CustomFieldDataType.Text, NewFieldRequired);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        NewFieldName = string.Empty;
        NewFieldRequired = false;
        StatusMessage = "Поле добавлено";
        await LoadFieldDefinitionsAsync();
    }

    [RelayCommand]
    private async Task ArchiveSelectedFieldAsync()
    {
        if (SelectedFieldDefinition is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var fields = scope.ServiceProvider.GetRequiredService<ICustomFieldService>();
        var result = await fields.ArchiveAsync(SelectedFieldDefinition.Id);
        StatusMessage = result.IsFailure ? result.Error : "Поле архивировано";
        await LoadFieldDefinitionsAsync();
    }

    [RelayCommand]
    private void ToggleFieldManager() => ShowFieldManager = !ShowFieldManager;

    private async Task ReloadAllAsync()
    {
        await LoadCategoriesAsync();
        await LoadFieldDefinitionsAsync();
        await LoadPrintersAsync();
        await ReloadProductsAsync();
    }

    private async Task LoadPrintersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();
        var result = await service.ListAsync(includeInactive: false);
        Printers.Clear();
        if (result.IsFailure)
        {
            return;
        }

        foreach (var printer in result.Value)
        {
            Printers.Add(printer);
        }

        SelectedPrinter ??= Printers.FirstOrDefault(p => p.IsDefault) ?? Printers.FirstOrDefault();
    }

    private async Task ReloadProductsAsync()
    {
        _loadedCount = 0;
        Products.Clear();
        await LoadProductsPageAsync(append: false);
    }

    private async Task LoadProductsPageAsync(bool append)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var products = scope.ServiceProvider.GetRequiredService<IProductService>();
            var categoryId = SelectedCategory?.Id;
            var result = await products.SearchAsync(
                SearchText,
                categoryId,
                includeArchived: false,
                skip: append ? _loadedCount : 0,
                take: PageSize);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            if (!append)
            {
                Products.Clear();
                _loadedCount = 0;
            }

            foreach (var item in result.Value.Items)
            {
                Products.Add(item);
            }

            _loadedCount += result.Value.Items.Count;
            _totalCount = result.Value.TotalCount;
            CanLoadMore = _loadedCount < _totalCount;
            StatusMessage = $"Найдено: {_totalCount} (показано {_loadedCount})";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var result = await categories.GetTreeAsync();
        Categories.Clear();
        CategoryOptions.Clear();
        CategoryOptions.Add(new CategoryOptionVm(null, "(без категории)"));

        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        Categories.Add(new CategoryNodeVm(null, "Все товары", 0));
        foreach (var node in BuildTree(result.Value))
        {
            Categories.Add(node);
        }

        foreach (var c in result.Value.OrderBy(c => c.Name))
        {
            CategoryOptions.Add(new CategoryOptionVm(c.Id, c.Name));
        }
    }

    private async Task LoadFieldDefinitionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var fields = scope.ServiceProvider.GetRequiredService<ICustomFieldService>();
        var result = await fields.ListAsync();
        FieldDefinitions.Clear();
        if (result.IsFailure)
        {
            return;
        }

        foreach (var item in result.Value)
        {
            FieldDefinitions.Add(item);
        }
    }

    private async Task LoadCustomFieldEditorsAsync(Dictionary<Guid, string?>? values)
    {
        await LoadFieldDefinitionsAsync();
        CustomFields.Clear();
        foreach (var def in FieldDefinitions)
        {
            values ??= new Dictionary<Guid, string?>();
            values.TryGetValue(def.Id, out var value);
            CustomFields.Add(new CustomFieldEditVm(def.Id, def.Name, def.IsRequired, value));
        }
    }

    private static IEnumerable<CategoryNodeVm> BuildTree(IReadOnlyList<Category> all)
    {
        var lookup = all.ToLookup(c => c.ParentId);
        IEnumerable<CategoryNodeVm> Walk(Guid? parentId, int depth)
        {
            foreach (var c in lookup[parentId].OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
            {
                var indent = depth > 0 ? new string(' ', depth * 2) : string.Empty;
                yield return new CategoryNodeVm(c.Id, indent + c.Name, depth);
                foreach (var child in Walk(c.Id, depth + 1))
                {
                    yield return child;
                }
            }
        }

        return Walk(null, 0);
    }
}

public sealed class CategoryNodeVm
{
    public CategoryNodeVm(Guid? id, string title, int depth)
    {
        Id = id;
        Title = title;
        Depth = depth;
    }

    public Guid? Id { get; }
    public string Title { get; }
    public int Depth { get; }
}

public sealed class CategoryOptionVm
{
    public CategoryOptionVm(Guid? id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid? Id { get; }
    public string Name { get; }
}

public partial class CustomFieldEditVm : ObservableObject
{
    public CustomFieldEditVm(Guid definitionId, string name, bool isRequired, string? value)
    {
        DefinitionId = definitionId;
        Name = name;
        IsRequired = isRequired;
        _value = value;
    }

    public Guid DefinitionId { get; }
    public string Name { get; }
    public bool IsRequired { get; }
    public string DisplayName => IsRequired ? $"{Name} *" : Name;
    [ObservableProperty] private string? _value;
}
