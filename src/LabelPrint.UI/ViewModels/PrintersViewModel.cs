using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class PrintersViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;

    public PrintersViewModel(IServiceScopeFactory scopeFactory, IUiDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        Title = "Принтеры";
    }

    public ObservableCollection<PrinterListItemDto> Printers { get; } = new();

    public Array ProtocolOptions { get; } = Enum.GetValues<PrinterProtocol>();

    [ObservableProperty] private PrinterListItemDto? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isEditorOpen;

    [ObservableProperty] private Guid? _editingId;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private PrinterProtocol _editProtocol = PrinterProtocol.File;
    [ObservableProperty] private string _editConnectionString = string.Empty;
    [ObservableProperty] private double _editPaperWidthMm = 58;
    [ObservableProperty] private bool _editRotate90;
    [ObservableProperty] private int _editDpi = 203;
    [ObservableProperty] private int _editDarkness = 8;
    [ObservableProperty] private int _editSpeed = 4;
    [ObservableProperty] private bool _editIsDefault;
    [ObservableProperty] private string? _editNotes;

    [RelayCommand]
    private async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();
        var result = await service.ListAsync(includeInactive: false);
        Printers.Clear();
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        foreach (var item in result.Value)
        {
            Printers.Add(item);
        }

        StatusMessage = $"Принтеров: {result.Value.Count}";
    }

    [RelayCommand]
    private void NewVirtualPrinter()
    {
        EditingId = null;
        EditName = "Виртуальный (PNG)";
        EditProtocol = PrinterProtocol.File;
        EditConnectionString = string.Empty;
        EditPaperWidthMm = 58;
        EditRotate90 = false;
        EditDpi = 203;
        EditDarkness = 8;
        EditSpeed = 4;
        EditIsDefault = Printers.Count == 0;
        EditNotes = "PNG сохраняется в %LocalAppData%\\LabelPrintPro\\prints";
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (Selected is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();
        var result = await service.GetAsync(Selected.Id);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dto = result.Value;
        EditingId = dto.Id;
        EditName = dto.Name;
        EditProtocol = dto.Protocol;
        EditConnectionString = dto.ConnectionString;
        EditPaperWidthMm = dto.PaperWidthMm;
        EditRotate90 = dto.Rotate90;
        EditDpi = dto.Dpi;
        EditDarkness = dto.Darkness;
        EditSpeed = dto.Speed;
        EditIsDefault = dto.IsDefault;
        EditNotes = dto.Notes;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var dto = new PrinterUpsertDto
        {
            Name = EditName,
            Protocol = EditProtocol,
            ConnectionString = EditConnectionString,
            PaperWidthMm = EditPaperWidthMm,
            Rotate90 = EditRotate90,
            Dpi = EditDpi,
            Darkness = EditDarkness,
            Speed = EditSpeed,
            IsDefault = EditIsDefault,
            Notes = EditNotes
        };

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();

        if (EditingId is Guid id)
        {
            var update = await service.UpdateAsync(id, dto);
            if (update.IsFailure)
            {
                StatusMessage = update.Error;
                return;
            }
        }
        else
        {
            var create = await service.CreateAsync(dto);
            if (create.IsFailure)
            {
                StatusMessage = create.Error;
                return;
            }
        }

        IsEditorOpen = false;
        StatusMessage = "Сохранено";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        if (Selected is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();
        var result = await service.SetDefaultAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Принтер по умолчанию обновлён";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление принтера",
            $"Отключить принтер «{Selected.Name}»?",
            confirmText: "Отключить",
            cancelText: "Отмена");

        if (!confirmed)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrinterService>();
        var result = await service.ArchiveAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Принтер отключён";
        Selected = null;
        await LoadAsync();
    }
}
