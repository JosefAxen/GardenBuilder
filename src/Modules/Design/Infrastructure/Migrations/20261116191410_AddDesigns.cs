using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Design.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDesigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "design");

            migrationBuilder.CreateTable(
                name: "designs",
                schema: "design",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    working_copy = table.Column<string>(type: "jsonb", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_designs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "design_versions",
                schema: "design",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    design_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    scene = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_design_versions_designs_design_id",
                        column: x => x.design_id,
                        principalSchema: "design",
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_design_versions_design_id_number",
                schema: "design",
                table: "design_versions",
                columns: new[] { "design_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_designs_customer_id_updated_at",
                schema: "design",
                table: "designs",
                columns: new[] { "customer_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_versions",
                schema: "design");

            migrationBuilder.DropTable(
                name: "designs",
                schema: "design");
        }
    }
}
