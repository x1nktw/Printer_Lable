using FluentAssertions;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LabelPrint.Application.Tests;

public class OrderSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_Upserts_By_ExternalOrderId()
    {
        var uow = new InMemoryUnitOfWork();
        var provider = Substitute.For<IOrderProvider>();
        provider.GetNewOrdersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExternalOrderDto(
                "ext-100",
                "100",
                "Client",
                null,
                null,
                null,
                null,
                500m,
                DateTimeOffset.UtcNow,
                "new",
                null,
                new[]
                {
                    new ExternalOrderItemDto(null, "SKU-A", null, "Item A", 1, 500m, null)
                })
        });

        var sync = new OrderSyncService(
            uow,
            provider,
            new OrderMatchingService(uow),
            NullLogger<OrderSyncService>.Instance);

        var first = await sync.SyncAsync();
        first.Should().HaveCount(1);

        provider.GetNewOrdersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExternalOrderDto(
                "ext-100",
                "100-UPD",
                "Client Updated",
                null,
                null,
                null,
                null,
                600m,
                DateTimeOffset.UtcNow,
                "confirmed",
                null,
                new[]
                {
                    new ExternalOrderItemDto(null, "SKU-B", null, "Item B", 2, 300m, null)
                })
        });

        var second = await sync.SyncAsync();
        second.Should().BeEmpty();

        var stored = await uow.Orders.GetByExternalOrderIdAsync("ext-100");
        stored.Should().NotBeNull();
        stored!.Number.Should().Be("100-UPD");
        stored.Status.Should().Be(OrderStatus.Confirmed);
        stored.Items.Should().HaveCount(1);
        stored.Items.First().Sku.Should().Be("SKU-B");
    }

    [Fact]
    public async Task SyncAsync_Creates_Separate_Orders_For_Different_ExternalIds()
    {
        var uow = new InMemoryUnitOfWork();
        var provider = Substitute.For<IOrderProvider>();
        provider.GetNewOrdersAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            CreateExternal("ext-1", "1"),
            CreateExternal("ext-2", "2")
        });

        var sync = new OrderSyncService(
            uow,
            provider,
            new OrderMatchingService(uow),
            NullLogger<OrderSyncService>.Instance);

        var created = await sync.SyncAsync();
        created.Should().HaveCount(2);

        var search = await uow.Orders.SearchAsync(null, null, 0, 10);
        search.TotalCount.Should().Be(2);
    }

    private static ExternalOrderDto CreateExternal(string externalId, string number) =>
        new(
            externalId,
            number,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "new",
            null,
            new[] { new ExternalOrderItemDto(null, null, null, "Test", 1, null, null) });
}
