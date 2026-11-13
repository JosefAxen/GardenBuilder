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
        builder.Property(a => a.ArticleNumber).HasMaxLength(64);
        builder.HasIndex(a => a.ArticleNumber).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Unit).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.StockStatus).HasConversion<string>().HasMaxLength(32);
        builder.ComplexProperty(a => a.Price, price =>
        {
            price.Property(p => p.Amount).HasPrecision(18, 2);
            price.Property(p => p.Currency).HasMaxLength(3);
        });
    }
}
