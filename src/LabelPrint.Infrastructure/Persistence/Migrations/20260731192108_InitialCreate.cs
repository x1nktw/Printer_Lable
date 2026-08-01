using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelPrint.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DatabasePath = table.Column<string>(type: "TEXT", nullable: true),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoPrintOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRefreshOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrdersRefreshIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    FrontPadSecret = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FrontPadBaseUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FrontPadWebhookListenUrl = table.Column<string>(type: "TEXT", nullable: true),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: true),
                    BackupPath = table.Column<string>(type: "TEXT", nullable: true),
                    AutoBackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackupIntervalHours = table.Column<int>(type: "INTEGER", nullable: false),
                    HistoryRetentionMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultLabelWidthMm = table.Column<double>(type: "REAL", nullable: false),
                    DefaultLabelHeightMm = table.Column<double>(type: "REAL", nullable: false),
                    EditorSnapEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EditorGridSizeMm = table.Column<double>(type: "REAL", nullable: false),
                    MaxPrintRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Employee = table.Column<string>(type: "TEXT", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    OrderedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ReceivedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ProcessedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Protocol = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionString = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PaperWidthMm = table.Column<double>(type: "REAL", nullable: false),
                    Dpi = table.Column<int>(type: "INTEGER", nullable: false),
                    Darkness = table.Column<int>(type: "INTEGER", nullable: false),
                    Speed = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    PrinterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrinterName = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TemplateName = table.Column<string>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", nullable: true),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderNumber = table.Column<string>(type: "TEXT", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Copies = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    VariablesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    PinHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabelTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    WidthMm = table.Column<double>(type: "REAL", nullable: false),
                    HeightMm = table.Column<double>(type: "REAL", nullable: false),
                    Dpi = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreviewImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPrinterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystemPreset = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelTemplates_Printers_DefaultPrinterId",
                        column: x => x.DefaultPrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PriceAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PriceCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    WeightValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpireDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ShelfLifeDays = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderItemTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_LabelTemplates_DefaultTemplateId",
                        column: x => x.DefaultTemplateId,
                        principalTable: "LabelTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_LabelTemplates_OrderItemTemplateId",
                        column: x => x.OrderItemTemplateId,
                        principalTable: "LabelTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalProductId = table.Column<string>(type: "TEXT", nullable: true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    PositionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    IsPrinted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductCustomFields",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCustomFields", x => new { x.ProductId, x.FieldDefinitionId });
                    table.ForeignKey(
                        name: "FK_ProductCustomFields_CustomFieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "CustomFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductCustomFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PrinterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Copies = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequestedByName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    VariablesJson = table.Column<string>(type: "TEXT", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTransientFailure = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalOrderId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_LabelTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "LabelTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_Name",
                table: "CustomFieldDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_DefaultPrinterId",
                table: "LabelTemplates",
                column: "DefaultPrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_IsArchived",
                table: "LabelTemplates",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_Name",
                table: "LabelTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ExternalOrderId",
                table: "Orders",
                column: "ExternalOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderedAt",
                table: "Orders",
                column: "OrderedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_IsDefault",
                table: "Printers",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_CreatedAt",
                table: "PrintHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_PrintedAt_Status",
                table: "PrintHistory",
                columns: new[] { "PrintedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_PrinterId",
                table: "PrintHistory",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_ProductId",
                table: "PrintHistory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_ExternalOrderId",
                table: "PrintJobs",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_OrderId",
                table: "PrintJobs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_OrderItemId",
                table: "PrintJobs",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_PrinterId",
                table: "PrintJobs",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_ProductId",
                table: "PrintJobs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_RequestedByUserId",
                table: "PrintJobs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Status_Priority_CreatedAt",
                table: "PrintJobs",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_TemplateId",
                table: "PrintJobs",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCustomFields_FieldDefinitionId",
                table: "ProductCustomFields",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "Barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DefaultTemplateId",
                table: "Products",
                column: "DefaultTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_OrderItemTemplateId",
                table: "Products",
                column: "OrderItemTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Name",
                table: "Users",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "PrintHistory");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "ProductCustomFields");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "LabelTemplates");

            migrationBuilder.DropTable(
                name: "Printers");
        }
    }
}
