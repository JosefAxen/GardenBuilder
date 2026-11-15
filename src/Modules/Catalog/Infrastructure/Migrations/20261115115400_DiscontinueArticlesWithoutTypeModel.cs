using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GardenBuilder.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiscontinueArticlesWithoutTypeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows added before AddArticleCatalogFields got an empty type model, colour and category. They are not
            // valid articles, so hide them from the catalog. They are discontinued rather than deleted, because a saved
            // design may point at them, and an import with the same article number turns them into real articles.
            migrationBuilder.Sql("UPDATE catalog.articles SET stock_status = 'Discontinued' WHERE type_model = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data clean-up: nothing to undo.
        }
    }
}
