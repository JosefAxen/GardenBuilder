using System.Text.Json;
using System.Text.Json.Serialization;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Pricing;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>The job's input: the design to start from and what the customer asked for.</summary>
internal sealed record DesignProposalInput(Guid DesignId, ProposalWishes Wishes);

/// <summary>One finished proposal as stored in the job's result.</summary>
internal sealed record ProposalResult(
    string Name, string Motivation, JsonElement Scene, decimal Low, decimal High, bool OverBudget, IReadOnlyList<string> Dropped);

internal sealed record DesignProposalResult(IReadOnlyList<ProposalResult> Proposals);

/// <summary>
/// Makes two whole-garden proposals (spec sections 4–5): "Nära din stil" and "Lite djärvare". For each, Claude chooses
/// building blocks, the layout engine places them and the check decides; errors go back to Claude at most twice. What
/// still fails after that is dropped, and the proposal stays if at least half of it is left. The design is never
/// changed; the customer tries and chooses in the editor.
/// </summary>
internal sealed class DesignProposalHandler(
    IDesignWorkingCopies designs, ICatalogSelection catalog, IPriceEstimates prices, ISceneCheck sceneCheck) : IAiJobHandler
{
    public const string NoProposals = "Vi kunde inte ta fram förslag just nu.";
    public const int Retries = 2;

    /// <summary>Enums as their kebab-case names, like the scene format.</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public string Kind => AiJobKinds.DesignProposal;

    public async Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken)
    {
        var input = job.Input.Deserialize<DesignProposalInput>(Json) ?? throw new AiProviderException(NoProposals, retryable: false);
        var design = await designs.GetAsync(input.DesignId, cancellationToken) ?? throw new AiProviderException(NoProposals, retryable: false);
        var zones = Zones.Divide(design.Scene);
        if (zones.Count == 0)
        {
            throw new AiProviderException(NoProposals, retryable: false);
        }

        var selection = ArticleSelection.Select(await catalog.InStockAsync(cancellationToken), input.Wishes.Zone, zones);
        var byId = selection.ToDictionary(a => a.Id);
        var proposals = new List<ProposalResult>();
        foreach (var bolder in new[] { false, true })
        {
            try
            {
                if (await ProposeAsync(calls, design.Scene, zones, selection, byId, input.Wishes, bolder, cancellationToken) is { } proposal)
                {
                    proposals.Add(proposal);
                }
            }
            catch (AiJobStoppedException) when (proposals.Count > 0)
            {
                // The daily budget or the switch stopped the second proposal; the first one is still worth showing.
                break;
            }
        }

        return proposals.Count == 0
            ? throw new AiProviderException(NoProposals, retryable: false)
            : JsonSerializer.SerializeToElement(new DesignProposalResult(proposals), Json);
    }

    private async Task<ProposalResult?> ProposeAsync(
        IAiCalls calls, SceneDocument current, IReadOnlyList<Zone> zones, IReadOnlyList<CatalogArticle> selection,
        Dictionary<Guid, CatalogArticle> byId, ProposalWishes wishes, bool bolder, CancellationToken cancellationToken)
    {
        IReadOnlyList<BlockError>? errors = null;
        (ProposalAnswer Answer, IReadOnlyList<Block> Blocks, IReadOnlyList<BlockError> Errors)? last = null;
        for (var attempt = 0; attempt <= Retries; attempt++)
        {
            var result = await calls.TextAsync(
                ProposalPrompt.Version,
                new TextRequest(null, ProposalPrompt.Text(wishes, zones, selection, bolder, errors), ProposalPrompt.MaxTokens,
                    OutputSchema: ProposalPrompt.Schema),
                cancellationToken);
            if (Parse(result.Response.Text) is not { Blocks.Count: > 0 } answer)
            {
                errors = [new BlockError(-1, "Svaret gick inte att läsa som byggklossar.")];
                continue;
            }

            var blocks = answer.Blocks!.Select(b => b.ToBlock()).ToList();
            var (layout, price, found) = await CheckAsync(current, zones, blocks, byId, wishes, cancellationToken);
            if (found.Count == 0)
            {
                return ToResult(answer, layout, price, overBudget: false, dropped: []);
            }

            errors = found;
            last = (answer, blocks, found);
        }

        // Out of retries: drop what still fails, keep the proposal if at least half of it is left.
        if (last is not { } final || ProposalCheck.WithoutFailed(final.Blocks, final.Errors) is not { } left)
        {
            return null;
        }

        var (kept, keptPrice, remaining) = await CheckAsync(current, zones, left, byId, wishes, cancellationToken);
        if (remaining.Any(e => !ProposalCheck.IsBudget(e)))
        {
            return null;
        }

        var dropped = final.Errors.Where(e => !ProposalCheck.IsBudget(e)).Select(e => e.Message).Distinct().ToList();
        return ToResult(final.Answer, kept, keptPrice, overBudget: remaining.Count > 0, dropped);
    }

    private async Task<(Layout Layout, PriceSpan Price, IReadOnlyList<BlockError> Errors)> CheckAsync(
        SceneDocument current, IReadOnlyList<Zone> zones, IReadOnlyList<Block> blocks, Dictionary<Guid, CatalogArticle> byId,
        ProposalWishes wishes, CancellationToken cancellationToken)
    {
        var layout = LayoutEngine.Build(current, zones, blocks, byId);
        var price = await prices.EstimateAsync(layout.Scene, cancellationToken);
        var errors = ProposalCheck.Errors(current, layout, blocks, byId, zones, wishes, sceneCheck.Check(layout.Scene), price);
        return (layout, price, errors);
    }

    private static ProposalResult ToResult(ProposalAnswer answer, Layout layout, PriceSpan price, bool overBudget, IReadOnlyList<string> dropped) => new(
        string.IsNullOrWhiteSpace(answer.Name) ? "Förslag" : answer.Name.Trim(),
        answer.Motivation?.Trim() ?? string.Empty,
        JsonSerializer.SerializeToElement(layout.Scene, SceneJson.Options),
        price.Low.Amount,
        price.High.Amount,
        overBudget,
        dropped);

    private static ProposalAnswer? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<ProposalAnswer>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
