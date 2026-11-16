using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Garden.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGardens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "garden");

            migrationBuilder.CreateTable(
                name: "gardens",
                schema: "garden",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gardens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gardens_customer_id",
                schema: "garden",
                table: "gardens",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gardens",
                schema: "garden");
        }
    }
}
