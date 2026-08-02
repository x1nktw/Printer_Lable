using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelPrint.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccentColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "#10A37F");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "AppSettings");
        }
    }
}
