using System.Text.Json;
using GardenBuilder.Modules.Ai.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GardenBuilder.Modules.Ai.Infrastructure;

internal sealed class AiCallConfiguration : IEntityTypeConfiguration<AiCall>
{
    public void Configure(EntityTypeBuilder<AiCall> builder)
    {
        builder.ToTable("calls");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.PromptVersion).HasMaxLength(64);
        builder.Property(c => c.Provider).HasMaxLength(32);
        builder.Property(c => c.Model).HasMaxLength(64);
        builder.Property(c => c.Outcome).HasMaxLength(16);
        builder.Property(c => c.ErrorType).HasMaxLength(200);
        builder.ComplexProperty(c => c.Cost, cost =>
        {
            cost.Property(p => p.Amount).HasPrecision(18, 2);
            cost.Property(p => p.Currency).HasMaxLength(3);
        });
        builder.HasIndex(c => c.StartedAt);
        builder.HasIndex(c => c.JobId);
    }
}

/// <summary>The one row in ai.settings. Quotas and switches are JSON maps from job kind.</summary>
internal sealed class AiSettingsRecord
{
    public const int SingleId = 1;

    public int Id { get; set; } = SingleId;

    public decimal DailyBudget { get; set; }

    public Dictionary<string, int> Quotas { get; set; } = [];

    public Dictionary<string, bool> Enabled { get; set; } = [];
}

internal sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettingsRecord>
{
    public void Configure(EntityTypeBuilder<AiSettingsRecord> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.DailyBudget).HasPrecision(18, 2);
        builder.Property(s => s.Quotas).HasColumnType("jsonb").HasConversion(Json<int>(), JsonComparer<int>());
        builder.Property(s => s.Enabled).HasColumnType("jsonb").HasConversion(Json<bool>(), JsonComparer<bool>());
    }

    /// <summary>Stores a map as JSON text; Npgsql needs an explicit opt-in to write dictionaries itself.</summary>
    private static ValueConverter<Dictionary<string, T>, string> Json<T>() => new(
        map => JsonSerializer.Serialize(map, JsonSerializerOptions.Default),
        text => JsonSerializer.Deserialize<Dictionary<string, T>>(text, JsonSerializerOptions.Default) ?? new());

    private static ValueComparer<Dictionary<string, T>> JsonComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, JsonSerializerOptions.Default) == JsonSerializer.Serialize(b, JsonSerializerOptions.Default),
        map => JsonSerializer.Serialize(map, JsonSerializerOptions.Default).GetHashCode(StringComparison.Ordinal),
        map => new Dictionary<string, T>(map));
}
