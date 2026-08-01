using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class HistoryViewModel : PageViewModelBase
{
    private const int PageSize = 50;
    private readonly IServiceScopeFactory _scopeFactory;
    private string? _nextCursor;

    public HistoryViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "История";
    }

    public ObservableCollection<PrintHistoryItemDto> Entries { get; } = new();

    [ObservableProperty] private PrintHistoryItemDto? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canLoadMore;

    [RelayCommand]
    private async Task LoadAsync()
    {
        _nextCursor = null;
        Entries.Clear();
        CanLoadMore = false;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !CanLoadMore)
        {
            return;
        }

        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task ReprintSelectedAsync()
    {
        if (Selected is null)
        {
            StatusMessage = "Выберите запись для повторной печати.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrintHistoryService>();
        var result = await service.ReprintAsync(Selected.Id);
        StatusMessage = result.IsFailure
            ? result.Error
            : $"Повторная печать поставлена в очередь ({result.Value})";
    }

    private async Task LoadPageAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPrintHistoryService>();
            var result = await service.GetPageAsync(_nextCursor, PageSize);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            foreach (var entry in result.Value.Items)
            {
                Entries.Add(entry);
            }

            _nextCursor = result.Value.NextCursor;
            CanLoadMore = result.Value.HasMore;
            StatusMessage = $"Записей: {Entries.Count}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
