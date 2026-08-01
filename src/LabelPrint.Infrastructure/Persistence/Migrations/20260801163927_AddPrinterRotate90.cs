using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelPrint.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterRotate90 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Rotate90",
                table: "Printers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rotate90",
                table: "Printers");
        }
    }
}
