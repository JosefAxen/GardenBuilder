using System.Security.Claims;
using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Features.Settings;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Ai.Features.Admin;

/// <summary>
/// The AI box in admin (docs/superpowers/specs/2026-11-17-ai-jobs-design.md, "Testjobbet i admin" and "API"): the
/// connection test, its status, and the settings with today's cost. Administrators only.
/// </summary>
internal static class AiAdminEndpoints
{
    public sealed record JobResponse(Guid Id, string Kind, string Status, int Attempts, JsonElement? Result, string? Failure);

    public sealed record SettingsBody(decimal DailyBudget, Dictionary<string, int> Quotas, Dictionary<string, bool> Enabled);

    public sealed record SettingsResponse(decimal DailyBudget, decimal TodayCost, IReadOnlyDictionary<string, int> Quotas, IReadOnlyDictionary<string, bool> Enabled);

    public sealed record CodeResponse(string Code);

    public sealed record ProblemsResponse(string Code, IReadOnlyList<string> Problems);

    public static RouteGroupBuilder MapAiAdmin(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup(string.Empty).RequireAuthorization(StaffPolicies.Admin);
        admin.MapPost("/connection-test", StartConnectionTestAsync).WithName("StartAiConnectionTest");
        admin.MapGet("/jobs/{id:guid}", GetJobAsync).WithName("GetAiJob");
        admin.MapGet("/settings", GetSettingsAsync).WithName("GetAiSettings");
        admin.MapPut("/settings", SaveSettingsAsync).WithName("SaveAiSettings");
        return group;
    }

    private static async Task<IResult> StartConnectionTestAsync(ClaimsPrincipal user, AiJobs jobs, CancellationToken cancellationToken)
    {
        var staffId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        try
        {
            var jobId = await jobs.EnqueueAsync(
                AiJobKinds.ConnectionTest, null, null, staffId, JsonSerializer.SerializeToElement(new { }), cancellationToken);
            return TypedResults.Accepted($"/api/ai/jobs/{jobId}", new { id = jobId });
        }
        catch (AiJobRefusedException e)
        {
            return TypedResults.Conflict(new CodeResponse(e.Code));
        }
    }

    private static async Task<IResult> GetJobAsync(Guid id, JobQueue queue, CancellationToken cancellationToken) =>
        await queue.FindAsync(id, cancellationToken) is { } job
            ? TypedResults.Ok(new JobResponse(job.Id, job.Kind, AiJobConfiguration.StatusText(job.Status), job.Attempts, job.Result, job.Failure))
            : TypedResults.NotFound();

    private static async Task<IResult> GetSettingsAsync(AiSettingsStore store, CostLog costs, CancellationToken cancellationToken)
    {
        var settings = await store.GetAsync(cancellationToken);
        var today = await costs.TodayAsync(cancellationToken);
        return TypedResults.Ok(new SettingsResponse(settings.DailyBudget.Amount, today.Amount, settings.Quotas, settings.Enabled));
    }

    private static async Task<IResult> SaveSettingsAsync(SettingsBody body, AiSettingsStore store, CancellationToken cancellationToken)
    {
        var settings = new AiSettings(Money.InSek(body.DailyBudget), body.Quotas ?? [], body.Enabled ?? []);
        var problems = settings.Validate();
        if (problems.Count > 0)
        {
            return TypedResults.BadRequest(new ProblemsResponse("invalid-settings", problems));
        }

        await store.SaveAsync(settings, cancellationToken);
        return TypedResults.Ok();
    }
}
