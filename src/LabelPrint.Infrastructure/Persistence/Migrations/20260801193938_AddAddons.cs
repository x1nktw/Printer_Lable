using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelPrint.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MatchAliases = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IconKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addons_IsArchived",
                table: "Addons",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Addons_Name",
                table: "Addons",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addons");
        }
    }
}
