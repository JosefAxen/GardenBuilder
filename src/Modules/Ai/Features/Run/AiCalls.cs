using System.Diagnostics;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Features.Settings;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Features.Run;

/// <summary>The calls one job makes: guards first, then the model, then a row in ai.calls whatever happened.</summary>
internal sealed partial class AiCalls(
    AiJob job,
    JobQueue queue,
    ITextModel model,
    IImageModel imageModel,
    AiGuards guards,
    CostLog costs,
    AiSettingsStore settings,
    IOptions<AiPricingOptions> pricing,
    TimeProvider time,
    ILogger logger) : IAiCalls
{
    public const string CutOff = "Svaret från AI:n blev avklippt.";
    public const string Refused = "AI:n avböjde att svara.";
    public const string TimedOut = "Claude svarade inte i tid.";
    public const string ImageTimedOut = "Bildtjänsten svarade inte i tid.";

    /// <summary>The last day the 80 % warning was logged, so it is logged once a day per Worker.</summary>
    private static DateTimeOffset _warnedFor;

    public async Task<AiTextResult> TextAsync(string promptVersion, TextRequest request, CancellationToken cancellationToken)
    {
        var modelName = model.ModelFor(job.Kind);
        request = request with { Model = modelName };
        if (await guards.RefuseCallAsync(job.Kind, modelName, cancellationToken) is { } refusal)
        {
            throw new AiJobStoppedException(refusal);
        }

        await queue.RenewLeaseAsync(job.Id, job.Attempts, cancellationToken);
        var started = time.GetUtcNow();
        var clock = Stopwatch.StartNew();
        TextResponse response;
        try
        {
            response = await model.CompleteAsync(request, cancellationToken);
        }
        catch (AiProviderException e)
        {
            await LogAsync(model.Provider, promptVersion, modelName, started, clock, 0, 0, Money.InSek(0m), "error", e.InnerException?.GetType().Name ?? e.GetType().Name, cancellationToken);
            throw;
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // Nobody asked to stop, so the provider timed out. Worth another attempt.
            await LogAsync(model.Provider, promptVersion, modelName, started, clock, 0, 0, Money.InSek(0m), "error", e.GetType().Name, cancellationToken);
            throw new AiProviderException(TimedOut, retryable: true, e);
        }

        // The price was checked before the call; the answering model is normally the same one.
        var cost = AiCost.Of(response.Model, response.InputTokens, response.OutputTokens, pricing.Value)
            ?? AiCost.Of(modelName, response.InputTokens, response.OutputTokens, pricing.Value)
            ?? Money.InSek(0m);
        var outcome = response.StopReason switch
        {
            "max_tokens" => "cut-off",
            "refusal" => "refused",
            _ => "ok",
        };
        var durationMs = await LogAsync(
            model.Provider, promptVersion, response.Model, started, clock, response.InputTokens, response.OutputTokens, cost, outcome, null, cancellationToken);

        return outcome switch
        {
            "cut-off" => throw new AiProviderException(CutOff, retryable: true),
            "refused" => throw new AiProviderException(Refused, retryable: true),
            _ => new AiTextResult(response, durationMs, cost),
        };
    }

    public async Task<ImageResponse> ImageAsync(string promptVersion, ImageRequest request, CancellationToken cancellationToken)
    {
        var modelName = imageModel.ModelFor(job.Kind);
        request = request with { Model = modelName };
        if (await guards.RefuseCallAsync(job.Kind, modelName, cancellationToken) is { } refusal)
        {
            throw new AiJobStoppedException(refusal);
        }

        await queue.RenewLeaseAsync(job.Id, job.Attempts, cancellationToken);
        var started = time.GetUtcNow();
        var clock = Stopwatch.StartNew();
        ImageResponse response;
        try
        {
            response = await imageModel.EditAsync(request, cancellationToken);
        }
        catch (AiProviderException e)
        {
            await LogAsync(imageModel.Provider, promptVersion, modelName, started, clock, 0, 0, Money.InSek(0m), "error", e.InnerException?.GetType().Name ?? e.GetType().Name, cancellationToken);
            throw;
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // Nobody asked to stop, so the provider timed out. Worth another attempt.
            await LogAsync(imageModel.Provider, promptVersion, modelName, started, clock, 0, 0, Money.InSek(0m), "error", e.GetType().Name, cancellationToken);
            throw new AiProviderException(ImageTimedOut, retryable: true, e);
        }

        var cost = AiCost.OfImage(response.Model, pricing.Value) ?? AiCost.OfImage(modelName, pricing.Value) ?? Money.InSek(0m);
        await LogAsync(imageModel.Provider, promptVersion, response.Model, started, clock, 0, 0, cost, "ok", null, cancellationToken);
        return response;
    }

    private async Task<int> LogAsync(
        string provider, string promptVersion, string usedModel, DateTimeOffset started, Stopwatch clock, int inputTokens, int outputTokens,
        Money cost, string outcome, string? errorType, CancellationToken cancellationToken)
    {
        var durationMs = (int)clock.ElapsedMilliseconds;
        await costs.AddAsync(
            AiCall.Create(job.Id, promptVersion, provider, usedModel, started, durationMs, inputTokens, outputTokens, cost, outcome, errorType),
            cancellationToken);
        await WarnNearBudgetAsync(cancellationToken);
        return durationMs;
    }

    private async Task WarnNearBudgetAsync(CancellationToken cancellationToken)
    {
        var today = CostLog.StartOfToday(time.GetUtcNow());
        if (_warnedFor == today)
        {
            return;
        }

        var budget = (await settings.GetAsync(cancellationToken)).DailyBudget;
        var spent = await costs.TodayAsync(cancellationToken);
        if (budget.Amount > 0 && spent.Amount >= budget.Amount * 0.8m)
        {
            _warnedFor = today;
            LogNearBudget(logger, spent, budget);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI cost today is {Spent}, at least 80 % of the daily budget {Budget}")]
    private static partial void LogNearBudget(ILogger logger, Money spent, Money budget);
}
