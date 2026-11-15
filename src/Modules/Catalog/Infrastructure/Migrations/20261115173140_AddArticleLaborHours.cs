using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleLaborHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "labor_hours",
                schema: "catalog",
                table: "articles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "labor_hours",
                schema: "catalog",
                table: "articles");
        }
    }
}
