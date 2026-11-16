using GardenBuilder.Contracts.Designs;
using GardenBuilder.Modules.Design.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Design.Infrastructure;

internal sealed class DesignConfiguration : IEntityTypeConfiguration<Domain.Design>
{
    public void Configure(EntityTypeBuilder<Domain.Design> builder)
    {
        builder.ToTable("designs");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.WorkingCopy).HasColumnType("jsonb");
        builder.HasIndex(d => new { d.CustomerId, d.UpdatedAt });
    }
}

internal sealed class DesignVersionConfiguration : IEntityTypeConfiguration<DesignVersion>
{
    public void Configure(EntityTypeBuilder<DesignVersion> builder)
    {
        builder.ToTable("design_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Scene).HasColumnType("jsonb");
        builder.Property(v => v.Reason).HasMaxLength(32).HasConversion(
            reason => ToText(reason),
            text => FromText(text));
        builder.HasIndex(v => new { v.DesignId, v.Number }).IsUnique();
        // Frozen versions are never removed as a side effect; a quote request may point at one.
        builder.HasOne<Domain.Design>().WithMany().HasForeignKey(v => v.DesignId).OnDelete(DeleteBehavior.Restrict);
    }

    private static string ToText(DesignVersionReason reason) => reason switch
    {
        DesignVersionReason.AiProposal => "ai-proposal",
        DesignVersionReason.QuoteRequest => "quote-request",
        DesignVersionReason.StaffRevision => "staff-revision",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    private static DesignVersionReason FromText(string text) => text switch
    {
        "ai-proposal" => DesignVersionReason.AiProposal,
        "quote-request" => DesignVersionReason.QuoteRequest,
        "staff-revision" => DesignVersionReason.StaffRevision,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, null),
    };
}
