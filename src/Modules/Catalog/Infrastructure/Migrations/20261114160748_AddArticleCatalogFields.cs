using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accent_color",
                schema: "catalog",
                table: "articles",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "catalog",
                table: "articles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "clip_height",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                schema: "catalog",
                table: "articles",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "growth_cm_per_year",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "hardiness_zone",
                schema: "catalog",
                table: "articles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "height",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "light",
                schema: "catalog",
                table: "articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "mature_height",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "mature_width",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "scale",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "type_model",
                schema: "catalog",
                table: "articles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "variety",
                schema: "catalog",
                table: "articles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "width",
                schema: "catalog",
                table: "articles",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accent_color",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "clip_height",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "color",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "growth_cm_per_year",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "hardiness_zone",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "height",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "light",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "mature_height",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "mature_width",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "scale",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "type_model",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "variety",
                schema: "catalog",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "width",
                schema: "catalog",
                table: "articles");
        }
    }
}
