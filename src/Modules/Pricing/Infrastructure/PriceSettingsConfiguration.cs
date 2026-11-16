using GardenBuilder.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GardenBuilder.Modules.Pricing.Infrastructure;

/// <summary>The settings table, seeded with the made-up defaults until admin can change them (delprojekt 11).</summary>
internal sealed class PriceSettingsConfiguration : IEntityTypeConfiguration<PriceSettingsRecord>
{
    public void Configure(EntityTypeBuilder<PriceSettingsRecord> builder)
    {
        builder.ToTable("price_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        foreach (var property in builder.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal)))
        {
            property.SetPrecision(10);
            property.SetScale(2);
        }

        var defaults = PriceSettings.Defaults;
        builder.HasData(new PriceSettingsRecord
        {
            Id = PriceSettingsRecord.SingleId,
            HourlyRate = defaults.HourlyRate.Amount,
            VatPercent = defaults.VatPercent,
            EstimatedRangePercent = defaults.EstimatedRangePercent,
            CustomerRangePercent = defaults.CustomerRangePercent,
            VerifiedRangePercent = defaults.VerifiedRangePercent,
            Establishment = defaults.Establishment.Amount,
            Transport = defaults.Transport.Amount,
            Disposal = defaults.Disposal.Amount,
        });
    }
}

internal sealed class RemovalNormConfiguration : IEntityTypeConfiguration<RemovalNormRecord>
{
    public void Configure(EntityTypeBuilder<RemovalNormRecord> builder)
    {
        builder.ToTable("removal_norms");
        builder.HasKey(n => n.RemovalClass);
        builder.Property(n => n.RemovalClass).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.Hours).HasPrecision(5, 2);
        builder.HasData(PriceSettings.Defaults.RemovalHours.Select(p => new RemovalNormRecord { RemovalClass = p.Key, Hours = p.Value }));
    }
}
