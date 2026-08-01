using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Plugins.Abstractions.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Placeholder for capabilities not in the public FrontPad API (no order list pull).
/// </summary>
public sealed class NullOrderProvider : IOrderProvider, IOrderProviderStatus
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NullOrderProvider(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public string ProviderKey => "frontpad";

    /// <inheritdoc />
    public Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<ExternalOrderDto>)Array.Empty<ExternalOrderDto>());

    /// <inheritdoc />
    public Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public string GetStatusMessage()
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Settings.GetAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(settings.FrontPadSecret))
        {
            return "Укажите секрет FrontPad в Настройках.";
        }

        return "FrontPad API: get_products / webhook. Список заказов методом API не предусмотрен.";
    }
}
