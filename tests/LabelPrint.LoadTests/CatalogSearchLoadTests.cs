using System.Diagnostics;
using FluentAssertions;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Services;
using LabelPrint.Application.Validation;
using LabelPrint.Domain.Entities;
using LabelPrint.Infrastructure.Persistence;
using LabelPrint.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.LoadTests;

/// <summary>
/// Catalog search performance smoke tests (~1k products). Target for 100k catalog documented in ROADMAP.
/// </summary>
public sealed class CatalogSearchLoadTests : IAsyncLifetime
{
    private const int SeedCount = 1000;
    private string _dbPath = null!;
    private LabelPrintDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"labelprint-load-{Guid.NewGuid():N}.db");
        _db = CreateContext(_dbPath);
        await _db.Database.MigrateAsync();
        await SeedProductsAsync();
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
    public async Task Search_1000_Products_Completes_Under_Two_Seconds()
    {
        var uow = new UnitOfWork(_db);
        var service = new ProductService(uow, new ProductUpsertDtoValidator(), NullLogger<ProductService>.Instance);

        var sw = Stopwatch.StartNew();
        var result = await service.SearchAsync("SKU-", null, includeArchived: false, skip: 0, take: 100);
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().BeGreaterThanOrEqualTo(SeedCount);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "catalog search over ~1k products should stay under 2s (100k target tracked in ROADMAP)");
    }

    private async Task SeedProductsAsync()
    {
        for (var i = 0; i < SeedCount; i++)
        {
            _db.Products.Add(new Product
            {
                Name = $"Load product {i}",
                Sku = $"SKU-{i:D6}",
                Barcode = $"460{i:D10}",
                PriceAmount = i % 1000
            });
        }

        await _db.SaveChangesAsync();
    }

    private static LabelPrintDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<LabelPrintDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new LabelPrintDbContext(options);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temp files.
        }
    }
}
