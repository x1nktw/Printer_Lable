using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Marking;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class RawMaterialsViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _suppressTemplatePersist;
    private bool _suppressFilterCascade;
    private IReadOnlyList<Category> _allCategories = Array.Empty<Category>();
    private IReadOnlyList<Guid> _markingCategoryIds = Array.Empty<Guid>();

    public RawMaterialsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "Маркировка";
    }

    public ObservableCollection<ProductListItemDto> Items { get; } = new();
    public ObservableCollection<PrinterListItemDto> Printers { get; } = new();
    public ObservableCollection<TemplateListItemDto> Templates { get; } = new();
    public ObservableCollection<CategoryOptionVm> FilterRootOptions { get; } = new();
    public ObservableCollection<CategoryOptionVm> FilterSubcategoryOptions { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private ProductListItemDto? _selectedItem;
    [ObservableProperty] private string _customName = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private PrinterListItemDto? _selectedPrinter;
    [ObservableProperty] private TemplateListItemDto? _selectedTemplate;
    [ObservableProperty] private bool _useCustomLabelDateTime;
    [ObservableProperty] private DateTime? _labelDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? _labelTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private CategoryOptionVm? _filterRootOption;
    [ObservableProperty] private CategoryOptionVm? _filterSubcategoryOption;

    public bool HasFilterSubcategories => FilterRootOption?.Id is not null;

    public string? ShelfLifeHint
    {
        get
        {
            if (SelectedItem is null)
            {
                return null;
            }

            var parts = new List<string>();
            if (SelectedItem.ShelfLifeDisplay is { Length: > 0 } shelf)
            {
                parts.Add($"Срок: {shelf}");
            }

            if (SelectedItem.TemperatureRegime is { Length: > 0 } temp)
            {
                parts.Add($"t°: {temp}");
            }

            if (SelectedItem.CategoryName is { Length: > 0 } cat)
            {
                parts.Add(cat);
            }

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    public bool HasShelfLifeHint => ShelfLifeHint is not null;

    partial void OnSelectedItemChanged(ProductListItemDto? value)
    {
        if (value is not null)
        {
            CustomName = value.Name;
        }

        OnPropertyChanged(nameof(ShelfLifeHint));
        OnPropertyChanged(nameof(HasShelfLifeHint));
    }

    partial void OnFilterRootOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressFilterCascade)
        {
            return;
        }

        RebuildFilterSubcategories(value?.Id);
        OnPropertyChanged(nameof(HasFilterSubcategories));
        _ = LoadAsync();
    }

    partial void OnFilterSubcategoryOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressFilterCascade)
        {
            return;
        }

        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Items.Clear();
        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var printers = scope.ServiceProvider.GetRequiredService<IPrinterService>();

        await EnsureFiltersAsync(categories);

        var filterIds = ResolveFilterIds();
        var search = await products.SearchAsync(
            SearchText,
            categoryId: null,
            includeArchived: false,
            skip: 0,
            take: 200,
            categoryIds: filterIds.Count > 0 ? filterIds : null);
        if (search.IsFailure)
        {
            StatusMessage = search.Error;
            return;
        }

        foreach (var item in search.Value.Items)
        {
            Items.Add(item);
        }

        Printers.Clear();
        var printerList = await printers.ListAsync();
        if (printerList.IsSuccess)
        {
            foreach (var p in printerList.Value)
            {
                Printers.Add(p);
            }

            SelectedPrinter ??= Printers.FirstOrDefault(p => p.IsDefault) ?? Printers.FirstOrDefault();
        }

        await LoadTemplatesAsync(scope);

        StatusMessage = Items.Count == 0
            ? "Нет позиций маркировки. Добавьте в Каталог → Маркировка или «Создать примеры»."
            : $"Позиций: {Items.Count}";
    }

    private async Task EnsureFiltersAsync(ICategoryService categories)
    {
        var tree = await categories.GetTreeAsync();
        if (tree.IsFailure)
        {
            return;
        }

        _allCategories = tree.Value;
        _markingCategoryIds = MarkingCategories.GetAllMarkingCategoryIds(_allCategories);

        if (FilterRootOptions.Count == 0)
        {
            _suppressFilterCascade = true;
            FilterRootOptions.Clear();
            FilterRootOptions.Add(new CategoryOptionVm(null, "Все категории"));
            foreach (var root in _allCategories
                         .Where(c => c.ParentId is null && MarkingCategories.IsMarkingRootName(c.Name))
                         .OrderBy(c => c.SortOrder)
                         .ThenBy(c => c.Name))
            {
                FilterRootOptions.Add(new CategoryOptionVm(root.Id, root.Name));
            }

            FilterRootOption ??= FilterRootOptions.FirstOrDefault();
            RebuildFilterSubcategories(FilterRootOption?.Id);
            _suppressFilterCascade = false;
        }
    }

    private void RebuildFilterSubcategories(Guid? rootId)
    {
        FilterSubcategoryOptions.Clear();
        FilterSubcategoryOptions.Add(new CategoryOptionVm(null, "Все подкатегории"));
        if (rootId is Guid rid)
        {
            foreach (var child in _allCategories
                         .Where(c => c.ParentId == rid)
                         .OrderBy(c => c.SortOrder)
                         .ThenBy(c => c.Name))
            {
                FilterSubcategoryOptions.Add(new CategoryOptionVm(child.Id, child.Name));
            }
        }

        _suppressFilterCascade = true;
        FilterSubcategoryOption = FilterSubcategoryOptions.FirstOrDefault();
        _suppressFilterCascade = false;
        OnPropertyChanged(nameof(HasFilterSubcategories));
    }

    private IReadOnlyList<Guid> ResolveFilterIds()
    {
        if (FilterSubcategoryOption?.Id is Guid subId)
        {
            return MarkingCategories.GetSelfAndDescendantIds(_allCategories, [subId]);
        }

        if (FilterRootOption?.Id is Guid rootId)
        {
            return MarkingCategories.GetSelfAndDescendantIds(_allCategories, [rootId]);
        }

        return _markingCategoryIds;
    }

    private async Task LoadTemplatesAsync(IServiceScope scope)
    {
        var templates = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var result = await templates.SearchAsync(null, includeArchived: false, skip: 0, take: 200);
        Templates.Clear();
        if (result.IsFailure)
        {
            return;
        }

        foreach (var item in result.Value.Items)
        {
            Templates.Add(item);
        }

        Guid? preferredId = null;
        var settings = await settingsSvc.GetAsync();
        if (settings.IsSuccess)
        {
            preferredId = settings.Value.MarkingPrintTemplateId;
        }

        _suppressTemplatePersist = true;
        try
        {
            SelectedTemplate = (preferredId is Guid id
                                   ? Templates.FirstOrDefault(t => t.Id == id)
                                   : null)
                               ?? Templates.FirstOrDefault(t =>
                                   t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))
                               ?? Templates.FirstOrDefault();
        }
        finally
        {
            _suppressTemplatePersist = false;
        }

        if (SelectedTemplate is not null && preferredId != SelectedTemplate.Id)
        {
            await PersistSelectedTemplateAsync(SelectedTemplate.Id);
        }
    }

    partial void OnSelectedTemplateChanged(TemplateListItemDto? value)
    {
        if (_suppressTemplatePersist || value is null)
        {
            return;
        }

        _ = PersistSelectedTemplateAsync(value.Id);
    }

    private async Task PersistSelectedTemplateAsync(Guid templateId)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var current = await settingsSvc.GetAsync();
        if (current.IsFailure)
        {
            return;
        }

        var dto = current.Value;
        if (dto.MarkingPrintTemplateId == templateId)
        {
            return;
        }

        dto.MarkingPrintTemplateId = templateId;
        await settingsSvc.SaveAsync(dto);
    }

    [RelayCommand]
    private async Task SeedSamplesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var templates = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        var tree = await categories.GetTreeAsync();
        var all = tree.IsSuccess ? tree.Value : Array.Empty<Category>();

        foreach (var rootName in MarkingCategories.Roots)
        {
            if (MarkingCategories.FindByName(all, rootName) is null)
            {
                await categories.CreateAsync(rootName, null);
            }
        }

        tree = await categories.GetTreeAsync();
        all = tree.IsSuccess ? tree.Value : Array.Empty<Category>();
        var rawId = MarkingCategories.FindByName(all, MarkingCategories.Raw);

        Guid? templateId = null;
        var tmpl = await templates.SearchAsync("Сырьё", includeArchived: false, skip: 0, take: 10);
        if (tmpl.IsSuccess)
        {
            templateId = tmpl.Value.Items
                .FirstOrDefault(t => t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        var samples = new (string Name, string Sku, string Temp)[]
        {
            ("Мясо", "RAW-MEAT", "+2…+6 °C"),
            ("Курица", "RAW-CHICKEN", "+2…+6 °C"),
            ("Рыба", "RAW-FISH", "0…+4 °C"),
            ("Томаты", "RAW-TOMATO", "+2…+6 °C"),
            ("Лук", "RAW-ONION", "комнатная"),
            ("Огурцы", "RAW-CUCUMBER", "+2…+6 °C"),
            ("Сыр", "RAW-CHEESE", "+2…+6 °C")
        };

        var createdCount = 0;
        foreach (var (name, sku, temp) in samples)
        {
            Guid? catId = rawId;
            var existing = await products.SearchAsync(sku, catId, false, 0, 5);
            if (existing.IsSuccess && existing.Value.Items.Any(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var result = await products.CreateAsync(new ProductUpsertDto
            {
                Name = name,
                Sku = sku,
                PriceAmount = 0,
                CategoryId = catId,
                TemperatureRegime = temp,
                DefaultTemplateId = templateId
            });
            if (result.IsSuccess)
            {
                createdCount++;
            }
        }

        FilterRootOptions.Clear();
        StatusMessage = createdCount > 0 ? $"Добавлено примеров: {createdCount}" : "Примеры уже есть.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        var name = !string.IsNullOrWhiteSpace(CustomName) ? CustomName.Trim() : SelectedItem?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Выберите позицию или введите название.";
            return;
        }

        DateTimeOffset? overrideDt = null;
        if (UseCustomLabelDateTime)
        {
            var date = (LabelDate ?? DateTime.Today).Date;
            var time = LabelTime ?? TimeSpan.Zero;
            overrideDt = new DateTimeOffset(date.Add(time));
        }

        using var scope = _scopeFactory.CreateScope();
        var print = scope.ServiceProvider.GetRequiredService<IPrintService>();
        var result = await print.PrintRawLabelAsync(
            name,
            SelectedPrinter?.Id,
            copies: 1,
            labelDateTimeOverride: overrideDt,
            productId: SelectedItem?.Id,
            templateId: SelectedTemplate?.Id);
        StatusMessage = result.IsFailure
            ? result.Error
            : $"Напечатано: {name} (задание {result.Value})";
    }
}
