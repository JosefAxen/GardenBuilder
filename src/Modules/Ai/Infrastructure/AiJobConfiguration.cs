using GardenBuilder.Modules.Ai.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Ai.Infrastructure;

internal sealed class AiJobConfiguration : IEntityTypeConfiguration<AiJob>
{
    public void Configure(EntityTypeBuilder<AiJob> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Kind).HasMaxLength(64);
        builder.Property(j => j.Status).HasMaxLength(16).HasConversion(s => StatusText(s), t => StatusFrom(t));
        builder.Property(j => j.Input).HasColumnType("jsonb");
        builder.Property(j => j.Result).HasColumnType("jsonb");
        builder.Property(j => j.Failure).HasMaxLength(500);
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
        builder.HasIndex(j => new { j.GardenId, j.Kind });
    }

    /// <summary>The status as stored: the raw SQL in JobQueue uses the same words.</summary>
    public static string StatusText(AiJobStatus status) => status switch
    {
        AiJobStatus.Queued => "queued",
        AiJobStatus.Running => "running",
        AiJobStatus.Succeeded => "succeeded",
        _ => "failed",
    };

    private static AiJobStatus StatusFrom(string text) => text switch
    {
        "queued" => AiJobStatus.Queued,
        "running" => AiJobStatus.Running,
        "succeeded" => AiJobStatus.Succeeded,
        "failed" => AiJobStatus.Failed,
        _ => throw new InvalidOperationException($"Unknown AI job status '{text}'."),
    };
}
