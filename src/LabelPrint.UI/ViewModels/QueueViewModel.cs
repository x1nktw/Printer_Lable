using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class QueueViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public QueueViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "Очередь печати";
    }

    public ObservableCollection<PrintQueueItemDto> Jobs { get; } = new();

    [ObservableProperty] private PrintQueueItemDto? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
            var result = await service.ListAsync();
            Jobs.Clear();
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            foreach (var job in result.Value)
            {
                Jobs.Add(job);
            }

            StatusMessage = $"В очереди: {Jobs.Count}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelSelectedAsync()
    {
        if (Selected is null || !Selected.CanCancel)
        {
            StatusMessage = "Выберите задание, которое можно отменить.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
        var result = await service.CancelAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Задание отменено";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RetrySelectedAsync()
    {
        if (Selected is null || !Selected.CanRetry)
        {
            StatusMessage = "Выберите задание с временным сбоем для повтора.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
        var result = await service.RetryAsync(Selected.Id);
        StatusMessage = result.IsFailure ? result.Error : "Задание возвращено в очередь";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ReprintSelectedAsync()
    {
        if (Selected is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
        var result = await service.ReprintJobAsync(Selected.Id);
        StatusMessage = result.IsFailure
            ? result.Error
            : $"Повторная печать поставлена в очередь ({result.Value})";
        await LoadAsync();
    }
}
