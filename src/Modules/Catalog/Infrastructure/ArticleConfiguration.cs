using GardenBuilder.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Catalog.Infrastructure;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ArticleNumber).HasMaxLength(ArticleData.MaxArticleNumberLength);
        builder.HasIndex(a => a.ArticleNumber).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(ArticleData.MaxNameLength);
        builder.Property(a => a.Variety).HasMaxLength(ArticleData.MaxVarietyLength);
        builder.Property(a => a.Category).HasMaxLength(ArticleData.MaxCategoryLength);
        builder.Property(a => a.Unit).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.StockStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.TypeModel).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Color).HasMaxLength(7);
        builder.Property(a => a.AccentColor).HasMaxLength(7);
        builder.Property(a => a.LaborHours).HasPrecision(5, 2);

        // Stored as the flag bits, e.g. sun and part shade = 3.
        builder.Property(a => a.Light).HasConversion<int>();
        builder.ComplexProperty(a => a.Price, price =>
        {
            price.Property(p => p.Amount).HasPrecision(18, 2);
            price.Property(p => p.Currency).HasMaxLength(3);
        });
    }
}
