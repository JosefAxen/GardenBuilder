using GardenBuilder.Modules.Garden.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Garden.Infrastructure;

internal sealed class GardenPhotoConfiguration : IEntityTypeConfiguration<GardenPhoto>
{
    public void Configure(EntityTypeBuilder<GardenPhoto> builder)
    {
        builder.ToTable("photos");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.GardenId);
        builder.Property(p => p.BlobName).HasMaxLength(200);
        builder.Ignore(p => p.Camera);
        builder.HasOne<Domain.Garden>().WithMany().HasForeignKey(p => p.GardenId).OnDelete(DeleteBehavior.Cascade);
    }
}
