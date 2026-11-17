using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Catalog.Infrastructure.Migrations
{
    /// <summary>
    /// The type models became forms (docs/superpowers/specs/2026-11-17-model-library-design.md). Old rows can only be
    /// moved by their old name, so thujas become spruces until the catalog is imported again.
    /// </summary>
    public partial class RenameTypeModelsToForms : Migration
    {
        private static readonly (string Old, string New)[] Renames =
        [
            ("DeciduousTree", "TreeLarge"),
            ("Conifer", "Spruce"),
            ("Shrub", "ShrubRound"),
            ("Perennial", "PerennialTall"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (old, form) in Renames)
            {
                migrationBuilder.Sql($"UPDATE catalog.articles SET type_model = '{form}' WHERE type_model = '{old}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Every form goes back to the old name of its family.
            migrationBuilder.Sql("UPDATE catalog.articles SET type_model = 'DeciduousTree' WHERE type_model IN ('TreeLarge', 'TreeFruit', 'TreeSmall');");
            migrationBuilder.Sql("UPDATE catalog.articles SET type_model = 'Conifer' WHERE type_model IN ('Pine', 'Spruce', 'ConiferColumn');");
            migrationBuilder.Sql("UPDATE catalog.articles SET type_model = 'Shrub' WHERE type_model IN ('ShrubRound', 'ShrubFlowering');");
            migrationBuilder.Sql("UPDATE catalog.articles SET type_model = 'Perennial' WHERE type_model IN ('PerennialTall', 'PerennialLow', 'FoliagePlant', 'OrnamentalGrass');");
            migrationBuilder.Sql("UPDATE catalog.articles SET type_model = 'Furniture' WHERE type_model = 'Rock';");
        }
    }
}
