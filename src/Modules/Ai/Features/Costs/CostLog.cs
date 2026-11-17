using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.Costs;

/// <summary>The cost log in ai.calls, and what today has cost so far.</summary>
internal sealed class CostLog(AiDbContext db, TimeProvider time)
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public async Task AddAsync(AiCall call, CancellationToken cancellationToken)
    {
        db.Add(call);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The sum of every call since midnight in Stockholm, summer time included.</summary>
    public async Task<Money> TodayAsync(CancellationToken cancellationToken)
    {
        var since = StartOfToday(time.GetUtcNow());
        var amounts = await db.Set<AiCall>()
            .Where(c => c.StartedAt >= since)
            .Select(c => c.Cost.Amount)
            .ToListAsync(cancellationToken);
        return Money.InSek(amounts.Sum());
    }

    /// <summary>Local midnight in Stockholm for the day <paramref name="now"/> falls on, as UTC.</summary>
    public static DateTimeOffset StartOfToday(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Stockholm);
        var midnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, Stockholm.GetUtcOffset(midnight)).ToUniversalTime();
    }
}
