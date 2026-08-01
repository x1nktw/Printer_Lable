using FluentAssertions;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Services;
using LabelPrint.Application.Validation;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Infrastructure.Persistence;
using LabelPrint.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelPrint.Infrastructure.Tests;

public class PersistenceIntegrationTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private LabelPrintDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"labelprint-test-{Guid.NewGuid():N}.db");
        _db = CreateContext(_dbPath);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-shm");
        TryDelete(_dbPath + "-wal");
    }

    [Fact]
    public async Task Migrate_Creates_Schema_And_Allows_Product_Crud()
    {
        var uow = new UnitOfWork(_db);
        var service = new ProductService(uow, new ProductUpsertDtoValidator(), NullLogger<ProductService>.Instance);

        var created = await service.CreateAsync(new ProductUpsertDto
        {
            Name = "Пицца",
            Sku = "PZ-100",
            Barcode = "4600000000001",
            PriceAmount = 599
        });

        created.IsSuccess.Should().BeTrue();

        var loaded = await uow.Products.GetBySkuAsync("PZ-100");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Пицца");
    }

    [Fact]
    public async Task Sku_Unique_Index_Is_Enforced_By_Database()
    {
        _db.Products.Add(new Product { Name = "A", Sku = "DUP", PriceAmount = 1 });
        await _db.SaveChangesAsync();

        _db.Products.Add(new Product { Name = "B", Sku = "DUP", PriceAmount = 2 });
        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DatabaseInitializer_Seeds_Users_And_Presets()
    {
        var options = Options.Create(new DatabaseOptions
        {
            DatabasePath = _dbPath,
            BackupRetentionCount = 3
        });

        var initializer = new DatabaseInitializer(_db, options, NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync();

        (await _db.Users.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        (await _db.LabelTemplates.CountAsync(t => t.IsSystemPreset)).Should().Be(7);
        (await _db.LabelTemplates.AnyAsync(t => t.Name == "Сырьё 58×40")).Should().BeTrue();
        (await _db.LabelTemplates.AnyAsync(t => t.Name == "Кухня чек 40×58")).Should().BeTrue();
        (await _db.Categories.AnyAsync(c => c.Name == "Сырьё" && !c.IsArchived)).Should().BeTrue();
        (await _db.Products.CountAsync(p => p.Sku.StartsWith("RAW-"))).Should().BeGreaterThanOrEqualTo(7);
        (await _db.AppSettings.CountAsync()).Should().Be(1);
        var settings = await _db.AppSettings.FirstAsync();
        settings.LabelDateTimeMode.Should().Be(LabelDateTimeMode.Realtime);
    }

    [Fact]
    public async Task PrintHistory_Uses_Keyset_Pagination()
    {
        var uow = new UnitOfWork(_db);
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-3);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var t3 = DateTimeOffset.UtcNow.AddMinutes(-1);

        await uow.PrintHistory.AddAsync(new PrintHistory { Description = "a", PrintedAt = t1 });
        await uow.PrintHistory.AddAsync(new PrintHistory { Description = "b", PrintedAt = t2 });
        await uow.PrintHistory.AddAsync(new PrintHistory { Description = "c", PrintedAt = t3 });
        await uow.SaveChangesAsync();

        var page = await uow.PrintHistory.GetPageAsync(before: null, pageSize: 2);
        page.Items.Should().HaveCount(2);
        page.HasMore.Should().BeTrue();
        page.Items[0].Description.Should().Be("c");
    }

    private static LabelPrintDbContext CreateContext(string path) =>
        new(new DbContextOptionsBuilder<LabelPrintDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup in CI/temp; do not fail the test run.
        }
    }
}
