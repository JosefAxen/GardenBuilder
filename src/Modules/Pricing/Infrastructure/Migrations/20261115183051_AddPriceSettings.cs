using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GardenBuilder.Modules.Pricing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.CreateTable(
                name: "price_settings",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    vat_percent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    estimated_range_percent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    customer_range_percent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    verified_range_percent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    establishment = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    transport = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    disposal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "removal_norms",
                schema: "pricing",
                columns: table => new
                {
                    removal_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_removal_norms", x => x.removal_class);
                });

            migrationBuilder.InsertData(
                schema: "pricing",
                table: "price_settings",
                columns: new[] { "id", "customer_range_percent", "disposal", "establishment", "estimated_range_percent", "hourly_rate", "transport", "vat_percent", "verified_range_percent" },
                values: new object[] { 1, 15m, 2000m, 2500m, 30m, 650m, 1500m, 25m, 5m });

            migrationBuilder.InsertData(
                schema: "pricing",
                table: "removal_norms",
                columns: new[] { "removal_class", "hours" },
                values: new object[,]
                {
                    { "Bed", 0.3m },
                    { "Conifer", 2m },
                    { "DeciduousTree", 3m },
                    { "Decking", 0.5m },
                    { "Fence", 0.4m },
                    { "Grass", 0.2m },
                    { "Gravel", 0.3m },
                    { "Hedge", 0.8m },
                    { "Paving", 0.6m },
                    { "ShrubOrOther", 0.5m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_settings",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "removal_norms",
                schema: "pricing");
        }
    }
}
