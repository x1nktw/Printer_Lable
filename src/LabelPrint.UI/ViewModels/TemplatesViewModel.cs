using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class TemplatesViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;
    private Action<Guid>? _openEditor;

    public TemplatesViewModel(
        IServiceScopeFactory scopeFactory,
        IUiDialogService dialogs,
        Action<Guid>? openEditor = null)
    {
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        _openEditor = openEditor;
        Title = "Шаблоны";
    }

    public void BindOpenEditor(Action<Guid> openEditor) => _openEditor = openEditor;

    public ObservableCollection<TemplateListItemDto> Templates { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private TemplateListItemDto? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _newName = "Новый шаблон";
    [ObservableProperty] private double _newWidth = 58;
    [ObservableProperty] private double _newHeight = 40;

    [RelayCommand]
    private async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.SearchAsync(SearchText, includeArchived: false, skip: 0, take: 200);
        Templates.Clear();
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        foreach (var item in result.Value.Items)
        {
            Templates.Add(item);
        }

        StatusMessage = $"Шаблонов: {result.Value.TotalCount}";
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.CreateAsync(NewName, NewWidth, NewHeight);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        StatusMessage = "Создан";
        await LoadAsync();
        _openEditor?.Invoke(result.Value);
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (Selected is null)
        {
            return;
        }

        _openEditor?.Invoke(Selected.Id);
    }

    [RelayCommand]
    private async Task DuplicateSelectedAsync()
    {
        if (Selected is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.DuplicateAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Скопировано";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportSelectedJsonAsync()
    {
        if (Selected is null)
        {
            StatusMessage = "Выберите шаблон для экспорта.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var export = scope.ServiceProvider.GetRequiredService<IExportService>();
        var result = await export.ExportTemplateJsonAsync(Selected.Id);
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
        var safeName = string.Join("_", Selected.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(dir, $"template_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(path, result.Value);
        StatusMessage = $"JSON: {path}";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null)
        {
            return;
        }

        if (Selected.IsInUse)
        {
            StatusMessage = "Шаблон выбран в Заказах или Маркировке и его нельзя удалить.";
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление шаблона",
            $"Удалить шаблон «{Selected.Name}»?",
            confirmText: "Удалить",
            cancelText: "Отмена");

        if (!confirmed)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.ArchiveAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Шаблон удалён";
        Selected = null;
        await LoadAsync();
    }
}
