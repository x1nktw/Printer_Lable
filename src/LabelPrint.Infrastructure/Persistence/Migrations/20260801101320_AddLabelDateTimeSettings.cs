using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelPrint.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelDateTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabelDateTimeMode",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ManualLabelDateTime",
                table: "AppSettings",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelDateTimeMode",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ManualLabelDateTime",
                table: "AppSettings");
        }
    }
}
