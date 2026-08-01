using LabelPrint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LabelPrint.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for LabelPrint Pro.
/// </summary>
public sealed class LabelPrintDbContext : DbContext
{
    public LabelPrintDbContext(DbContextOptions<LabelPrintDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();

    public DbSet<ProductCustomField> ProductCustomFields => Set<ProductCustomField>();

    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();

    public DbSet<Printer> Printers => Set<Printer>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<PrintHistory> PrintHistory => Set<PrintHistory>();

    public DbSet<User> Users => Set<User>();

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabelPrintDbContext).Assembly);
        ApplySqliteDateTimeOffsetConversions(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// SQLite cannot ORDER BY DateTimeOffset; persist as UTC ticks (long).
    /// </summary>
    private static void ApplySqliteDateTimeOffsetConversions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
                            v => v.UtcTicks,
                            v => new DateTimeOffset(v, TimeSpan.Zero)));
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset?, long?>(
                            v => v.HasValue ? v.Value.UtcTicks : null,
                            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null));
                }
            }
        }
    }
}
