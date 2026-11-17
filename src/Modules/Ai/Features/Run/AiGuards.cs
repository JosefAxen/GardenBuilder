using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Settings;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Features.Run;

/// <summary>
/// The checks that keep AI cost under control (docs/superpowers/specs/2026-11-17-ai-jobs-design.md, "Kvoter och
/// dagsbudget"): the step's switch, the daily budget and the model's price.
/// </summary>
internal sealed class AiGuards(CostLog costs, AiSettingsStore settings, IOptions<AiPricingOptions> pricing)
{
    public const string StepOff = "Det här AI-steget är avstängt.";
    public const string BudgetReached = "AI-stegen är pausade för i dag.";

    public static string NoPrice(string model) => $"Modellen {model} saknar pris i inställningarna.";

    /// <summary>Why a call for this kind of job may not be made now, in Swedish; null when it may.</summary>
    public async Task<string?> RefuseCallAsync(string kind, string model, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (AiJobKinds.IsStep(kind) && !current.Enabled[kind])
        {
            return StepOff;
        }

        if (!pricing.Value.HasPrice(model))
        {
            return NoPrice(model);
        }

        return (await costs.TodayAsync(cancellationToken)).Amount >= current.DailyBudget.Amount ? BudgetReached : null;
    }

    /// <summary>Why the job may not be queued: "step-off", "budget-reached" or "quota-reached"; null when it may.</summary>
    public async Task<string?> RefuseEnqueueAsync(
        string kind, Guid? gardenId, Func<Task<int>> countForGarden, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (AiJobKinds.IsStep(kind) && !current.Enabled[kind])
        {
            return "step-off";
        }

        if ((await costs.TodayAsync(cancellationToken)).Amount >= current.DailyBudget.Amount)
        {
            return "budget-reached";
        }

        return AiJobKinds.IsStep(kind) && gardenId is not null && await countForGarden() >= current.Quotas[kind]
            ? "quota-reached"
            : null;
    }
}
