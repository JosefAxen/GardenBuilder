using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Domain;

/// <summary>
/// The daily budget for the whole service, the quota per garden for each AI step, and an on/off switch per step. Set
/// by administrators in admin.
/// </summary>
internal sealed record AiSettings(Money DailyBudget, IReadOnlyDictionary<string, int> Quotas, IReadOnlyDictionary<string, bool> Enabled)
{
    public const decimal MaxBudget = 100_000m;
    public const int MaxQuota = 100;

    public static AiSettings Defaults { get; } = new(
        Money.InSek(500m),
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [AiJobKinds.PhotoInterpretation] = 3,
            [AiJobKinds.DesignProposal] = 5,
            [AiJobKinds.DreamImage] = 5,
        },
        AiJobKinds.Steps.ToDictionary(step => step, _ => true, StringComparer.Ordinal));

    /// <summary>What is wrong, in Swedish; empty when the settings can be saved.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (DailyBudget.Currency != Money.Sek || DailyBudget.Amount is < 0 or > MaxBudget)
        {
            problems.Add("Dagsbudgeten ska vara mellan 0 och 100 000 kr.");
        }

        foreach (var step in AiJobKinds.Steps)
        {
            if (!Quotas.TryGetValue(step, out var quota) || quota is < 0 or > MaxQuota)
            {
                problems.Add($"Kvoten för {AiJobKinds.SwedishName(step)} ska vara mellan 0 och 100.");
            }
        }

        if (!AiJobKinds.Steps.All(Enabled.ContainsKey))
        {
            problems.Add("Alla tre AI-stegen måste anges.");
        }

        return problems;
    }
}
