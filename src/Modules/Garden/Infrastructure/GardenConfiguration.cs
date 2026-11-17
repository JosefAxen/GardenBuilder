using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Garden.Infrastructure;

internal sealed class GardenConfiguration : IEntityTypeConfiguration<Domain.Garden>
{
    public void Configure(EntityTypeBuilder<Domain.Garden> builder)
    {
        builder.ToTable("gardens");
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => g.CustomerId);
    }
}
