using LabelPrint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelPrint.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(64);
        builder.Property(x => x.PriceAmount).HasPrecision(18, 4);
        builder.Property(x => x.PriceCurrency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.WeightValue).HasPrecision(18, 6);
        builder.Property(x => x.TemperatureRegime).HasMaxLength(64);
        builder.Property(x => x.IconKey).HasMaxLength(64);

        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("Barcode IS NOT NULL");
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Name);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DefaultTemplate)
            .WithMany()
            .HasForeignKey(x => x.DefaultTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.OrderItemTemplate)
            .WithMany()
            .HasForeignKey(x => x.OrderItemTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.CustomFields)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.ParentId);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFieldDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ProductCustomFieldConfiguration : IEntityTypeConfiguration<ProductCustomField>
{
    public void Configure(EntityTypeBuilder<ProductCustomField> builder)
    {
        builder.ToTable("ProductCustomFields");
        builder.HasKey(x => new { x.ProductId, x.FieldDefinitionId });
        builder.Property(x => x.Value).HasMaxLength(2048);

        builder.HasOne(x => x.FieldDefinition)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.FieldDefinitionId);
    }
}

internal sealed class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
{
    public void Configure(EntityTypeBuilder<LabelTemplate> builder)
    {
        builder.ToTable("LabelTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentJson).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsArchived);

        builder.HasOne(x => x.DefaultPrinter)
            .WithMany(x => x.Templates)
            .HasForeignKey(x => x.DefaultPrinterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> builder)
    {
        builder.ToTable("Printers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ConnectionString).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.IsDefault);
    }
}

internal sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("PrintJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.VariablesJson).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(2048);
        builder.Property(x => x.ExternalOrderId).HasMaxLength(128);
        builder.Property(x => x.RequestedByName).HasMaxLength(128);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt });
        builder.HasIndex(x => x.PrinterId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.ExternalOrderId);

        builder.HasOne(x => x.Printer)
            .WithMany(x => x.PrintJobs)
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.OrderItem)
            .WithMany()
            .HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalOrderId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Number).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(256);
        builder.Property(x => x.CustomerPhone).HasMaxLength(64);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);

        builder.HasIndex(x => x.ExternalOrderId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OrderedAt);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Price).HasPrecision(18, 4);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PrintHistoryConfiguration : IEntityTypeConfiguration<PrintHistory>
{
    public void Configure(EntityTypeBuilder<PrintHistory> builder)
    {
        builder.ToTable("PrintHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.VariablesJson).IsRequired();
        builder.HasIndex(x => new { x.PrintedAt, x.Status });
        builder.HasIndex(x => x.PrinterId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PinHash).HasMaxLength(256);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FrontPadBaseUrl).HasMaxLength(512);
        builder.Property(x => x.FrontPadSecret).HasMaxLength(256);
        builder.Property(x => x.AccentColor).HasMaxLength(16);
    }
}

internal sealed class AddonConfiguration : IEntityTypeConfiguration<Addon>
{
    public void Configure(EntityTypeBuilder<Addon> builder)
    {
        builder.ToTable("Addons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MatchAliases).HasMaxLength(512);
        builder.Property(x => x.IconKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsArchived);
    }
}
