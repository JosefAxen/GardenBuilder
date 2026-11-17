using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.Settings;

/// <summary>The AI settings in ai.settings. Until an administrator saves them, the defaults apply.</summary>
internal sealed class AiSettingsStore(AiDbContext db)
{
    public async Task<AiSettings> GetAsync(CancellationToken cancellationToken)
    {
        var row = await db.Set<AiSettingsRecord>().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return AiSettings.Defaults;
        }

        // A step added after the row was saved gets its default.
        var quotas = new Dictionary<string, int>(AiSettings.Defaults.Quotas, StringComparer.Ordinal);
        var enabled = new Dictionary<string, bool>(AiSettings.Defaults.Enabled, StringComparer.Ordinal);
        foreach (var (step, quota) in row.Quotas)
        {
            quotas[step] = quota;
        }

        foreach (var (step, on) in row.Enabled)
        {
            enabled[step] = on;
        }

        return new AiSettings(Money.InSek(row.DailyBudget), quotas, enabled);
    }

    /// <summary>Saves valid settings; check <see cref="AiSettings.Validate"/> first.</summary>
    public async Task SaveAsync(AiSettings settings, CancellationToken cancellationToken)
    {
        var row = await db.Set<AiSettingsRecord>().FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new AiSettingsRecord();
            db.Add(row);
        }

        row.DailyBudget = settings.DailyBudget.Amount;
        row.Quotas = new Dictionary<string, int>(settings.Quotas, StringComparer.Ordinal);
        row.Enabled = new Dictionary<string, bool>(settings.Enabled, StringComparer.Ordinal);
        await db.SaveChangesAsync(cancellationToken);
    }
}
