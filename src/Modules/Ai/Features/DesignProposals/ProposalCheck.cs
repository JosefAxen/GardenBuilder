using System.Globalization;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Pricing;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>A proposal after the check: its scene, what is still wrong, and whether it ended up over budget.</summary>
internal sealed record CheckedProposal(SceneDocument Scene, IReadOnlyList<BlockError> Errors, bool OverBudget);

/// <summary>
/// Plain code that decides whether a proposal may reach the customer (spec section 4): the layout's own errors, the
/// scene rules, the selection, the hardiness zone, the light, that kept things are untouched, and the budget.
/// Every error is Swedish and points at the block that caused it (−1 for the whole proposal), so the model can fix it.
/// </summary>
internal static class ProposalCheck
{
    public const string KeptChanged = "Något som ska stå kvar har ändrats.";

    private const string BudgetStart = "Förslaget kostar cirka ";
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public static bool IsBudget(BlockError error) =>
        error is { Block: -1 } && error.Message.StartsWith(BudgetStart, StringComparison.Ordinal);

    public static IReadOnlyList<BlockError> Errors(
        SceneDocument current,
        Layout layout,
        IReadOnlyList<Block> blocks,
        IReadOnlyDictionary<Guid, CatalogArticle> selection,
        IReadOnlyList<Zone> zones,
        ProposalWishes wishes,
        IReadOnlyList<SceneProblem> sceneProblems,
        PriceSpan price)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(wishes);
        ArgumentNullException.ThrowIfNull(price);
        var errors = new List<BlockError>(layout.Errors);
        int BlockOf(Guid id) => layout.MadeBy.TryGetValue(id, out var block) ? block : -1;

        foreach (var element in layout.Scene.Elements.Where(e => e.Status == ElementStatus.New))
        {
            if (ArticleOf(element) is not { } articleId || !selection.TryGetValue(articleId, out var article))
            {
                errors.Add(new BlockError(BlockOf(element.Id), "Artikeln finns inte i urvalet."));
                continue;
            }

            if (!ArticleSelection.IsPlant(article))
            {
                continue;
            }

            if (!ArticleSelection.Survives(article, wishes.Zone))
            {
                errors.Add(new BlockError(BlockOf(element.Id), $"{article.Name} klarar inte växtzon {wishes.Zone}."));
            }
            else if (element is PointElement plant && ZoneAt(zones, plant.Position) is { } zone
                && !article.Light.Contains(ArticleSelection.LightKey(zone.Light)))
            {
                errors.Add(new BlockError(BlockOf(element.Id), $"{article.Name} trivs inte i {LightWord(zone.Light)} {zone.Name}."));
            }
        }

        if (!KeptUntouched(current, layout.Scene))
        {
            errors.Add(new BlockError(-1, KeptChanged));
        }

        errors.AddRange(sceneProblems.Select(p => new BlockError(
            p.ElementId is { } id ? BlockOf(id) : -1, $"Förslaget bryter mot regeln \"{p.Code}\".")));

        if (wishes.Ceiling is { } ceiling && price.Middle.Amount > ceiling)
        {
            errors.Add(new BlockError(-1, $"{BudgetStart}{Kronor(price.Middle.Amount)} kr men budgeten är högst {Kronor(ceiling)} kr."));
        }

        // One message per block and text is enough for the model.
        return [.. errors.Distinct()];
    }

    /// <summary>
    /// The blocks without the failed ones and those that hang on them (a path from a patio, furniture on it), or null
    /// when fewer than half are left. Errors about the whole proposal (block −1) drop nothing.
    /// </summary>
    public static IReadOnlyList<Block>? WithoutFailed(IReadOnlyList<Block> blocks, IReadOnlyList<BlockError> errors)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(errors);
        var failed = errors.Where(e => e.Block >= 0 && e.Block < blocks.Count).Select(e => e.Block).ToHashSet();
        var patioGone = blocks.Where((b, i) => b.Type == "patio" && failed.Contains(i)).Any()
            || !blocks.Any(b => b.Type == "patio");
        var left = blocks
            .Where((b, i) => !failed.Contains(i))
            .Where(b => !(patioGone && (b.Type == "furniture" || (b.Type == "path" && b.From == "patio"))))
            .ToList();
        return left.Count * 2 >= blocks.Count && left.Count > 0 ? left : null;
    }

    /// <summary>Everything the customer kept is still there, unchanged. Today's lawn may be cut, so lawns are not compared.</summary>
    private static bool KeptUntouched(SceneDocument current, SceneDocument proposal)
    {
        var after = proposal.Elements.ToDictionary(e => e.Id);
        return current.Elements
            .Where(e => e is not SurfaceElement { Material: SurfaceMaterial.Grass })
            .All(e => after.TryGetValue(e.Id, out var same) && same.Equals(e));
    }

    private static Guid? ArticleOf(SceneElement element) => element switch
    {
        PointElement p => p.ArticleId,
        LineElement l => l.ArticleId,
        SurfaceElement s => s.ArticleId,
        _ => null,
    };

    private static Zone? ZoneAt(IReadOnlyList<Zone> zones, Point2 point) =>
        zones.FirstOrDefault(z => z.Outline.Count(ring => new Polygon(ring).Contains(point)) % 2 == 1);

    private static string LightWord(ZoneLight light) => light switch
    {
        ZoneLight.Sun => "sol",
        ZoneLight.PartShade => "halvskugga",
        _ => "skugga",
    };

    /// <summary>"210 000" with a plain space, so the message reads the same everywhere.</summary>
    private static string Kronor(decimal amount) => amount.ToString("#,0", Swedish).Replace(' ', ' ');
}
