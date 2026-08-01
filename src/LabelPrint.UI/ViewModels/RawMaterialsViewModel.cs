using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class RawMaterialsViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RawMaterialsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "Маркировка";
    }

    public ObservableCollection<ProductListItemDto> Items { get; } = new();
    public ObservableCollection<PrinterListItemDto> Printers { get; } = new();
    public ObservableCollection<TemplateListItemDto> Templates { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private ProductListItemDto? _selectedItem;
    [ObservableProperty] private string _customName = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private PrinterListItemDto? _selectedPrinter;
    [ObservableProperty] private TemplateListItemDto? _selectedTemplate;
    [ObservableProperty] private bool _useCustomLabelDateTime;
    [ObservableProperty] private DateTime? _labelDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? _labelTime = DateTime.Now.TimeOfDay;

    partial void OnSelectedItemChanged(ProductListItemDto? value)
    {
        if (value is not null)
        {
            CustomName = value.Name;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Items.Clear();
        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var printers = scope.ServiceProvider.GetRequiredService<IPrinterService>();

        var tree = await categories.GetTreeAsync();
        Guid? rawCategoryId = null;
        if (tree.IsSuccess)
        {
            rawCategoryId = FindCategoryId(tree.Value, "Сырьё");
        }

        var search = await products.SearchAsync(
            SearchText,
            rawCategoryId,
            includeArchived: false,
            skip: 0,
            take: 200);
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
            ? "Нет товаров в категории «Сырьё». Добавьте в Каталог → Маркировка или «Создать примеры»."
            : $"Позиций: {Items.Count}";
    }

    private async Task LoadTemplatesAsync(IServiceScope scope)
    {
        var templates = scope.ServiceProvider.GetRequiredService<ITemplateService>();
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

        SelectedTemplate ??= Templates.FirstOrDefault(t =>
                                t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))
                            ?? Templates.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SeedSamplesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var templates = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        var tree = await categories.GetTreeAsync();
        Guid? catId = tree.IsSuccess ? FindCategoryId(tree.Value, "Сырьё") : null;
        if (catId is null)
        {
            var created = await categories.CreateAsync("Сырьё", null);
            if (created.IsFailure)
            {
                StatusMessage = created.Error;
                return;
            }

            catId = created.Value;
        }

        Guid? templateId = null;
        var tmpl = await templates.SearchAsync("Сырьё", includeArchived: false, skip: 0, take: 10);
        if (tmpl.IsSuccess)
        {
            templateId = tmpl.Value.Items
                .FirstOrDefault(t => t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        string[] names = ["Мясо", "Томаты", "Лук", "Сыр", "Огурцы", "Курица", "Рыба"];
        var createdCount = 0;
        foreach (var name in names)
        {
            var sku = $"RAW-{TranslitSku(name)}";
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
                DefaultTemplateId = templateId
            });
            if (result.IsSuccess)
            {
                createdCount++;
            }
        }

        StatusMessage = createdCount > 0 ? $"Добавлено примеров: {createdCount}" : "Примеры уже есть.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        var name = !string.IsNullOrWhiteSpace(CustomName) ? CustomName.Trim() : SelectedItem?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Выберите сырьё или введите название.";
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

    private static Guid? FindCategoryId(IReadOnlyList<Category> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return node.Id;
            }
        }

        foreach (var node in nodes)
        {
            if (node.Children.Count == 0)
            {
                continue;
            }

            var child = FindCategoryId(node.Children.ToList(), name);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static string TranslitSku(string name) => name switch
    {
        "Мясо" => "MEAT",
        "Томаты" => "TOMATO",
        "Лук" => "ONION",
        "Сыр" => "CHEESE",
        "Огурцы" => "CUCUMBER",
        "Курица" => "CHICKEN",
        "Рыба" => "FISH",
        _ => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
    };
}
