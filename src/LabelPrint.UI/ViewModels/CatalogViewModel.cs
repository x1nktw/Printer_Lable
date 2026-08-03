using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Marking;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class CatalogViewModel : PageViewModelBase
{
    private const int PageSize = 100;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;
    private int _loadedCount;
    private int _totalCount;
    private IReadOnlyList<Category> _allCategories = Array.Empty<Category>();
    private IReadOnlyList<Guid> _markingCategoryIds = Array.Empty<Guid>();
    private Guid? _editDefaultTemplateId;
    private Guid? _editOrderItemTemplateId;
    private DateOnly? _editExpireDate;
    private DateOnly? _editManufactureDate;
    private bool _suppressMarkingCascade;

    public CatalogViewModel(IServiceScopeFactory scopeFactory, IUiDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        Title = "Каталог";
        foreach (var preset in MarkingCategories.TemperaturePresets)
        {
            TemperaturePresets.Add(preset);
        }
    }

    public ObservableCollection<ProductListItemDto> Products { get; } = new();
    public ObservableCollection<CategoryOptionVm> CategoryOptions { get; } = new();
    public ObservableCollection<CategoryOptionVm> MarkingRootOptions { get; } = new();
    public ObservableCollection<CategoryOptionVm> MarkingSubcategoryOptions { get; } = new();
    public ObservableCollection<CategoryOptionVm> FilterMarkingRootOptions { get; } = new();
    public ObservableCollection<CategoryOptionVm> FilterMarkingSubcategoryOptions { get; } = new();
    public ObservableCollection<string> TemperaturePresets { get; } = new();
    public ObservableCollection<CustomFieldEditVm> CustomFields { get; } = new();
    public ObservableCollection<CustomFieldDefinitionDto> FieldDefinitions { get; } = new();
    public ObservableCollection<AddonListItemDto> Addons { get; } = new();
    public ObservableCollection<string> IconKeys { get; } = new();

    [ObservableProperty] private int _selectedSectionIndex;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private ProductListItemDto? _selectedProduct;
    [ObservableProperty] private ProductListItemDto? _selectedRawProduct;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canLoadMore;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editSku = string.Empty;
    [ObservableProperty] private string? _editBarcode;
    [ObservableProperty] private decimal? _editPrice;
    [ObservableProperty] private decimal? _editShelfLifeValue;
    [ObservableProperty] private ShelfLifeUnit _editShelfLifeUnit = ShelfLifeUnit.Days;
    [ObservableProperty] private string? _editTemperatureRegime;
    [ObservableProperty] private Guid? _editCategoryId;
    [ObservableProperty] private CategoryOptionVm? _editCategoryOption;
    [ObservableProperty] private CategoryOptionVm? _editMarkingRootOption;
    [ObservableProperty] private CategoryOptionVm? _editMarkingSubcategoryOption;
    [ObservableProperty] private CategoryOptionVm? _filterMarkingRootOption;
    [ObservableProperty] private CategoryOptionVm? _filterMarkingSubcategoryOption;
    [ObservableProperty] private string _newMarkingSubcategoryName = string.Empty;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private Guid? _editingId;
    [ObservableProperty] private string _newFieldName = string.Empty;
    [ObservableProperty] private bool _newFieldRequired;
    [ObservableProperty] private bool _showFieldManager;
    [ObservableProperty] private CustomFieldDefinitionDto? _selectedFieldDefinition;
    [ObservableProperty] private AddonListItemDto? _selectedAddon;
    [ObservableProperty] private bool _isAddonEditorOpen;
    [ObservableProperty] private Guid? _editingAddonId;
    [ObservableProperty] private string _editAddonName = string.Empty;
    [ObservableProperty] private string? _editAddonAliases;
    [ObservableProperty] private string _editAddonIconKey = "bullet";

    public bool IsProductsSection => SelectedSectionIndex == 0;
    public bool IsRawSection => SelectedSectionIndex == 1;
    public bool IsAddonsSection => SelectedSectionIndex == 2;
    public bool IsProductCatalogSection => SelectedSectionIndex is 0 or 1;
    public bool HasMarkingSubcategories => EditMarkingRootOption?.Id is not null;
    public bool HasFilterMarkingSubcategories => FilterMarkingRootOption?.Id is not null;
    public bool CanAddMarkingSubcategory =>
        FilterMarkingRootOption?.Id is not null
        || (IsEditorOpen && EditMarkingRootOption?.Id is not null);

    public IReadOnlyList<ShelfLifeUnitOptionVm> ShelfLifeUnitOptions { get; } =
    [
        new(ShelfLifeUnit.Days, "дней"),
        new(ShelfLifeUnit.Hours, "часов")
    ];

    public ShelfLifeUnitOptionVm EditShelfLifeUnitOption
    {
        get => ShelfLifeUnitOptions.First(o => o.Unit == EditShelfLifeUnit);
        set
        {
            if (value is null)
            {
                return;
            }

            EditShelfLifeUnit = value.Unit;
            OnPropertyChanged(nameof(EditShelfLifeUnitOption));
        }
    }

    private ProductListItemDto? CurrentSelectedProduct =>
        IsRawSection ? SelectedRawProduct : SelectedProduct;

    partial void OnSelectedSectionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsProductsSection));
        OnPropertyChanged(nameof(IsRawSection));
        OnPropertyChanged(nameof(IsAddonsSection));
        OnPropertyChanged(nameof(IsProductCatalogSection));
        IsEditorOpen = false;
        IsAddonEditorOpen = false;
        _ = ReloadForSectionAsync();
    }

    partial void OnEditCategoryOptionChanged(CategoryOptionVm? value) => EditCategoryId = value?.Id;

    partial void OnEditMarkingRootOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressMarkingCascade)
        {
            return;
        }

        RebuildMarkingSubcategoryOptions(value?.Id, selectSubId: null);
        EditCategoryId = ResolveMarkingCategoryId(EditMarkingRootOption, EditMarkingSubcategoryOption);
        OnPropertyChanged(nameof(HasMarkingSubcategories));
        OnPropertyChanged(nameof(CanAddMarkingSubcategory));
    }

    partial void OnEditMarkingSubcategoryOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressMarkingCascade)
        {
            return;
        }

        EditCategoryId = ResolveMarkingCategoryId(EditMarkingRootOption, value);
    }

    partial void OnFilterMarkingRootOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressMarkingCascade)
        {
            return;
        }

        RebuildFilterMarkingSubcategoryOptions(value?.Id);
        OnPropertyChanged(nameof(HasFilterMarkingSubcategories));
        OnPropertyChanged(nameof(CanAddMarkingSubcategory));
        if (IsRawSection)
        {
            _ = ReloadProductsAsync();
        }
    }

    partial void OnFilterMarkingSubcategoryOptionChanged(CategoryOptionVm? value)
    {
        if (_suppressMarkingCascade || !IsRawSection)
        {
            return;
        }

        _ = ReloadProductsAsync();
    }

    [RelayCommand]
    private async Task LoadAsync() => await ReloadAllAsync();

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !CanLoadMore || !IsProductCatalogSection)
        {
            return;
        }

        await LoadProductsPageAsync(append: true);
    }

    [RelayCommand]
    private async Task NewProductAsync()
    {
        EditingId = null;
        _editDefaultTemplateId = null;
        _editOrderItemTemplateId = null;
        EditName = string.Empty;
        EditSku = string.Empty;
        EditBarcode = null;
        EditPrice = 0;
        EditShelfLifeValue = null;
        EditShelfLifeUnit = ShelfLifeUnit.Days;
        OnPropertyChanged(nameof(EditShelfLifeUnitOption));
        EditTemperatureRegime = null;
        _editExpireDate = null;
        _editManufactureDate = null;
        if (IsRawSection)
        {
            var root = FilterMarkingRootOption?.Id is not null
                ? MarkingRootOptions.FirstOrDefault(c => c.Id == FilterMarkingRootOption.Id)
                : MarkingRootOptions.FirstOrDefault(c =>
                    c.Name.Equals(MarkingCategories.Raw, StringComparison.OrdinalIgnoreCase))
                  ?? MarkingRootOptions.FirstOrDefault();
            _suppressMarkingCascade = true;
            EditMarkingRootOption = root;
            RebuildMarkingSubcategoryOptions(root?.Id, FilterMarkingSubcategoryOption?.Id);
            _suppressMarkingCascade = false;
            EditCategoryId = ResolveMarkingCategoryId(EditMarkingRootOption, EditMarkingSubcategoryOption);
        }
        else
        {
            EditCategoryId = null;
            EditCategoryOption = CategoryOptions.FirstOrDefault(c => c.Id is null);
        }

        await LoadCustomFieldEditorsAsync(null);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        var selected = CurrentSelectedProduct;
        if (selected is null)
        {
            StatusMessage = "Выберите позицию в списке.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var result = await products.GetAsync(selected.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dto = result.Value;
        EditingId = selected.Id;
        EditName = dto.Name;
        EditSku = dto.Sku;
        EditBarcode = dto.Barcode;
        EditPrice = dto.PriceAmount;
        EditShelfLifeValue = dto.ShelfLifeDays;
        EditShelfLifeUnit = dto.ShelfLifeUnit;
        OnPropertyChanged(nameof(EditShelfLifeUnitOption));
        EditTemperatureRegime = dto.TemperatureRegime;
        _editExpireDate = dto.ExpireDate;
        _editManufactureDate = dto.ManufactureDate;
        EditCategoryId = dto.CategoryId;
        if (IsRawSection)
        {
            ApplyMarkingEditorSelection(dto.CategoryId);
        }
        else
        {
            EditCategoryOption = CategoryOptions.FirstOrDefault(c => c.Id == EditCategoryId);
        }

        _editDefaultTemplateId = dto.DefaultTemplateId;
        _editOrderItemTemplateId = dto.OrderItemTemplateId;
        await LoadCustomFieldEditorsAsync(dto.CustomFieldValues);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var sku = EditSku?.Trim() ?? string.Empty;
        if (IsRawSection && string.IsNullOrWhiteSpace(sku))
        {
            sku = $"RAW-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            EditSku = sku;
        }

        Guid? categoryId;
        if (IsRawSection)
        {
            categoryId = ResolveMarkingCategoryId(EditMarkingRootOption, EditMarkingSubcategoryOption);
            if (categoryId is null)
            {
                StatusMessage = "Выберите категорию маркировки.";
                return;
            }
        }
        else
        {
            categoryId = EditCategoryId;
        }

        var dto = new ProductUpsertDto
        {
            Name = EditName,
            Sku = sku,
            Barcode = EditBarcode,
            PriceAmount = EditPrice ?? 0,
            ShelfLifeDays = EditShelfLifeValue is > 0 ? (int)EditShelfLifeValue.Value : null,
            ShelfLifeUnit = EditShelfLifeUnit,
            TemperatureRegime = EditTemperatureRegime,
            ExpireDate = _editExpireDate,
            ManufactureDate = _editManufactureDate,
            CategoryId = categoryId,
            DefaultTemplateId = _editDefaultTemplateId,
            OrderItemTemplateId = _editOrderItemTemplateId,
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
        var selected = CurrentSelectedProduct;
        if (selected is null)
        {
            StatusMessage = "Выберите позицию в списке.";
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление",
            $"Удалить «{selected.Name}»?",
            confirmText: "Удалить",
            cancelText: "Отмена");
        if (!confirmed)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var result = await products.ArchiveAsync(selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Удалено";
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
        StatusMessage = result.IsFailure ? result.Error : "Поле удалено";
        await LoadFieldDefinitionsAsync();
    }

    [RelayCommand]
    private void ToggleFieldManager() => ShowFieldManager = !ShowFieldManager;

    [RelayCommand]
    private void NewAddon()
    {
        EditingAddonId = null;
        EditAddonName = string.Empty;
        EditAddonAliases = null;
        EditAddonIconKey = IconKeys.FirstOrDefault() ?? "bullet";
        IsAddonEditorOpen = true;
    }

    [RelayCommand]
    private void EditSelectedAddon()
    {
        if (SelectedAddon is null)
        {
            return;
        }

        EditingAddonId = SelectedAddon.Id;
        EditAddonName = SelectedAddon.Name;
        EditAddonAliases = SelectedAddon.MatchAliases;
        EditAddonIconKey = SelectedAddon.IconKey;
        if (!IconKeys.Contains(EditAddonIconKey, StringComparer.OrdinalIgnoreCase))
        {
            IconKeys.Add(EditAddonIconKey);
        }

        IsAddonEditorOpen = true;
    }

    [RelayCommand]
    private void CancelAddonEdit() => IsAddonEditorOpen = false;

    [RelayCommand]
    private async Task SaveAddonAsync()
    {
        var dto = new AddonUpsertDto
        {
            Name = EditAddonName,
            MatchAliases = EditAddonAliases,
            IconKey = EditAddonIconKey
        };

        using var scope = _scopeFactory.CreateScope();
        var addons = scope.ServiceProvider.GetRequiredService<IAddonService>();
        if (EditingAddonId is Guid id)
        {
            var update = await addons.UpdateAsync(id, dto);
            if (update.IsFailure)
            {
                StatusMessage = update.Error;
                return;
            }
        }
        else
        {
            var create = await addons.CreateAsync(dto);
            if (create.IsFailure)
            {
                StatusMessage = create.Error;
                return;
            }
        }

        IsAddonEditorOpen = false;
        StatusMessage = "Добавка сохранена";
        await LoadAddonsAsync();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAddonAsync()
    {
        if (SelectedAddon is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление",
            $"Удалить добавку «{SelectedAddon.Name}»?",
            confirmText: "Удалить",
            cancelText: "Отмена");
        if (!confirmed)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var addons = scope.ServiceProvider.GetRequiredService<IAddonService>();
        var result = await addons.ArchiveAsync(SelectedAddon.Id);
        StatusMessage = result.IsFailure ? result.Error : "Добавка удалена";
        await LoadAddonsAsync();
    }

    [RelayCommand]
    private async Task ImportAddonIconAsync()
    {
        var path = await _dialogs.PickPngFileAsync();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "addon-icons");
        Directory.CreateDirectory(dir);

        var stem = Path.GetFileNameWithoutExtension(path);
        stem = string.Join("_", stem.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = Guid.NewGuid().ToString("N")[..8];
        }

        var target = Path.Combine(dir, $"{stem}.png");
        File.Copy(path, target, overwrite: true);
        if (!IconKeys.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            IconKeys.Add(stem);
        }

        EditAddonIconKey = stem;
        StatusMessage = $"Иконка импортирована: {stem}.png";
    }

    private async Task ReloadAllAsync()
    {
        await LoadCategoriesAsync();
        await LoadFieldDefinitionsAsync();
        await LoadIconKeysAsync();
        await ReloadForSectionAsync();
    }

    private async Task ReloadForSectionAsync()
    {
        if (IsAddonsSection)
        {
            await LoadAddonsAsync();
            return;
        }

        if (IsRawSection)
        {
            await EnsureMarkingCategoriesAsync();
        }

        await ReloadProductsAsync();
    }

    private async Task EnsureMarkingCategoriesAsync()
    {
        await LoadCategoriesAsync();
        if (_markingCategoryIds.Count > 0
            && _allCategories.Any(c => c.ParentId is not null && MarkingCategories.IsMarkingCategory(c, _allCategories)))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        foreach (var rootName in MarkingCategories.Roots)
        {
            if (MarkingCategories.FindByName(_allCategories, rootName) is null)
            {
                await categories.CreateAsync(rootName, parentId: null);
            }
        }

        await LoadCategoriesAsync();
        foreach (var (rootName, children) in MarkingCategories.DefaultSubcategories)
        {
            var parentId = MarkingCategories.FindByName(_allCategories, rootName);
            if (parentId is not Guid pid)
            {
                continue;
            }

            foreach (var sub in children)
            {
                if (MarkingCategories.FindByName(_allCategories, sub, pid) is null)
                {
                    await categories.CreateAsync(sub, pid);
                }
            }
        }

        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task AddMarkingSubcategoryAsync()
    {
        var parentId = FilterMarkingRootOption?.Id
                       ?? (IsEditorOpen ? EditMarkingRootOption?.Id : null);
        var name = NewMarkingSubcategoryName?.Trim();
        if (parentId is null)
        {
            StatusMessage = "Сначала выберите корневую категорию.";
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Введите название подкатегории.";
            return;
        }

        if (MarkingCategories.FindByName(_allCategories, name, parentId) is not null)
        {
            StatusMessage = $"Подкатегория «{name}» уже есть.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var created = await categories.CreateAsync(name, parentId);
        if (created.IsFailure)
        {
            StatusMessage = created.Error;
            return;
        }

        NewMarkingSubcategoryName = string.Empty;
        StatusMessage = $"Добавлена подкатегория «{name}»";
        var selectedRootId = parentId;
        var selectedSubId = created.Value;
        await LoadCategoriesAsync();

        _suppressMarkingCascade = true;
        FilterMarkingRootOption = FilterMarkingRootOptions.FirstOrDefault(c => c.Id == selectedRootId);
        RebuildFilterMarkingSubcategoryOptions(selectedRootId);
        FilterMarkingSubcategoryOption = FilterMarkingSubcategoryOptions.FirstOrDefault(c => c.Id == selectedSubId);
        _suppressMarkingCascade = false;
        OnPropertyChanged(nameof(HasFilterMarkingSubcategories));
        OnPropertyChanged(nameof(CanAddMarkingSubcategory));

        if (IsEditorOpen && EditMarkingRootOption?.Id == selectedRootId)
        {
            RebuildMarkingSubcategoryOptions(selectedRootId, selectedSubId);
            EditCategoryId = selectedSubId;
        }

        await ReloadProductsAsync();
    }

    private async Task LoadAddonsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAddonService>();
        var result = await service.ListAsync();
        Addons.Clear();
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        foreach (var item in result.Value)
        {
            Addons.Add(item);
        }

        StatusMessage = $"Добавок: {Addons.Count}";
    }

    private async Task LoadIconKeysAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAddonService>();
        IconKeys.Clear();
        foreach (var key in service.BuiltInIconKeys)
        {
            IconKeys.Add(key);
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "addon-icons");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.png"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!IconKeys.Contains(stem, StringComparer.OrdinalIgnoreCase))
                {
                    IconKeys.Add(stem);
                }
            }
        }

        await Task.CompletedTask;
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

            IReadOnlyCollection<Guid>? categoryIds = null;
            IReadOnlyCollection<Guid>? excludeCategoryIds = null;
            if (IsRawSection)
            {
                categoryIds = ResolveMarkingFilterIds();
                if (categoryIds.Count == 0)
                {
                    StatusMessage = "Категории маркировки не найдены.";
                    return;
                }
            }
            else if (IsProductsSection)
            {
                excludeCategoryIds = _markingCategoryIds.Count > 0 ? _markingCategoryIds : null;
            }

            var result = await products.SearchAsync(
                SearchText,
                categoryId: null,
                includeArchived: false,
                skip: append ? _loadedCount : 0,
                take: PageSize,
                categoryIds: categoryIds,
                excludeCategoryIds: excludeCategoryIds);
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
        CategoryOptions.Clear();
        CategoryOptions.Add(new CategoryOptionVm(null, "(без категории)"));
        MarkingRootOptions.Clear();
        FilterMarkingRootOptions.Clear();
        FilterMarkingRootOptions.Add(new CategoryOptionVm(null, "Все категории"));

        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            _allCategories = Array.Empty<Category>();
            _markingCategoryIds = Array.Empty<Guid>();
            return;
        }

        _allCategories = result.Value;
        _markingCategoryIds = MarkingCategories.GetAllMarkingCategoryIds(_allCategories);

        foreach (var c in _allCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
        {
            if (MarkingCategories.IsMarkingCategory(c, _allCategories))
            {
                if (c.ParentId is null && MarkingCategories.IsMarkingRootName(c.Name))
                {
                    var option = new CategoryOptionVm(c.Id, c.Name);
                    MarkingRootOptions.Add(option);
                    FilterMarkingRootOptions.Add(option);
                }

                continue;
            }

            CategoryOptions.Add(new CategoryOptionVm(c.Id, c.Name));
        }

        _suppressMarkingCascade = true;
        FilterMarkingRootOption ??= FilterMarkingRootOptions.FirstOrDefault();
        RebuildFilterMarkingSubcategoryOptions(FilterMarkingRootOption?.Id);
        _suppressMarkingCascade = false;
    }

    private void RebuildMarkingSubcategoryOptions(Guid? rootId, Guid? selectSubId)
    {
        MarkingSubcategoryOptions.Clear();
        MarkingSubcategoryOptions.Add(new CategoryOptionVm(null, "(без подкатегории)"));
        if (rootId is Guid rid)
        {
            foreach (var child in _allCategories
                         .Where(c => c.ParentId == rid)
                         .OrderBy(c => c.SortOrder)
                         .ThenBy(c => c.Name))
            {
                MarkingSubcategoryOptions.Add(new CategoryOptionVm(child.Id, child.Name));
            }
        }

        _suppressMarkingCascade = true;
        EditMarkingSubcategoryOption = selectSubId is Guid sid
            ? MarkingSubcategoryOptions.FirstOrDefault(c => c.Id == sid)
            : MarkingSubcategoryOptions.FirstOrDefault();
        _suppressMarkingCascade = false;
        OnPropertyChanged(nameof(HasMarkingSubcategories));
    }

    private void RebuildFilterMarkingSubcategoryOptions(Guid? rootId)
    {
        FilterMarkingSubcategoryOptions.Clear();
        FilterMarkingSubcategoryOptions.Add(new CategoryOptionVm(null, "Все подкатегории"));
        if (rootId is Guid rid)
        {
            foreach (var child in _allCategories
                         .Where(c => c.ParentId == rid)
                         .OrderBy(c => c.SortOrder)
                         .ThenBy(c => c.Name))
            {
                FilterMarkingSubcategoryOptions.Add(new CategoryOptionVm(child.Id, child.Name));
            }
        }

        _suppressMarkingCascade = true;
        FilterMarkingSubcategoryOption = FilterMarkingSubcategoryOptions.FirstOrDefault();
        _suppressMarkingCascade = false;
        OnPropertyChanged(nameof(HasFilterMarkingSubcategories));
    }

    private void ApplyMarkingEditorSelection(Guid? categoryId)
    {
        if (categoryId is null)
        {
            _suppressMarkingCascade = true;
            EditMarkingRootOption = MarkingRootOptions.FirstOrDefault();
            RebuildMarkingSubcategoryOptions(EditMarkingRootOption?.Id, null);
            _suppressMarkingCascade = false;
            return;
        }

        var category = _allCategories.FirstOrDefault(c => c.Id == categoryId);
        if (category is null)
        {
            return;
        }

        Guid rootId;
        Guid? subId = null;
        if (category.ParentId is Guid parentId)
        {
            rootId = parentId;
            subId = category.Id;
        }
        else
        {
            rootId = category.Id;
        }

        _suppressMarkingCascade = true;
        EditMarkingRootOption = MarkingRootOptions.FirstOrDefault(c => c.Id == rootId);
        RebuildMarkingSubcategoryOptions(rootId, subId);
        _suppressMarkingCascade = false;
    }

    private static Guid? ResolveMarkingCategoryId(CategoryOptionVm? root, CategoryOptionVm? sub) =>
        sub?.Id ?? root?.Id;

    private IReadOnlyList<Guid> ResolveMarkingFilterIds()
    {
        if (FilterMarkingSubcategoryOption?.Id is Guid subId)
        {
            return MarkingCategories.GetSelfAndDescendantIds(_allCategories, [subId]);
        }

        if (FilterMarkingRootOption?.Id is Guid rootId)
        {
            return MarkingCategories.GetSelfAndDescendantIds(_allCategories, [rootId]);
        }

        return _markingCategoryIds;
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

public sealed class ShelfLifeUnitOptionVm
{
    public ShelfLifeUnitOptionVm(ShelfLifeUnit unit, string name)
    {
        Unit = unit;
        Name = name;
    }

    public ShelfLifeUnit Unit { get; }
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
