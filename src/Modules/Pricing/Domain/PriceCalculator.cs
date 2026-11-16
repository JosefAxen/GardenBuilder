using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Pricing.Domain;

/// <summary>
/// Turns a scene into an approximate price (docs/design.md, "Prissättning och offertflöde"). A pure function: scene,
/// article facts and settings in, estimate out. It reads no database, so every rule is easy to test.
/// </summary>
internal static class PriceCalculator
{
    private const string Hour = "hour";
    private const string Piece = "piece";

    public static PriceEstimate Estimate(SceneDocument scene, IReadOnlyDictionary<Guid, ArticlePriceFacts> articles, PriceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(articles);
        ArgumentNullException.ThrowIfNull(settings);

        var sloped = scene.Elements.OfType<PlotElement>().Any(p => p.Sloped);
        var lines = new LineCollector();
        var missing = 0;
        var confidence = Confidence.High;

        foreach (var element in scene.Elements)
        {
            if (Quantity(element) is not { } quantity)
            {
                continue; // The plot and buildings are not priced.
            }

            // Areas and lengths can be wrong, piece counts cannot.
            var measured = element is SurfaceElement or LineElement;
            var source = Effective(element.MeasurementSource, sloped);
            var percent = measured ? settings.RangePercent(source) : 0m;
            var article = UsableArticle(element, articles);

            if (element.Status == ElementStatus.New)
            {
                if (article is null || article.Discontinued)
                {
                    missing++;
                    continue;
                }

                lines.Add(LineKind.Material, article.Id.ToString(), article.Name, article.Unit, quantity, article.Price * quantity, percent);
                var hours = article.LaborHours * quantity;
                lines.Add(LineKind.Labor, article.Id.ToString(), $"Arbete: {article.Name}", Hour, hours, settings.HourlyRate * hours, percent);
            }
            else if (element.Status == ElementStatus.ExistingRemove)
            {
                var removalClass = ClassOf(element, article);
                var hours = settings.RemovalHours[removalClass] * quantity;
                lines.Add(
                    LineKind.Removal, removalClass.ToString(), $"Borttagning: {RemovalClasses.Name(removalClass)}", Hour, hours,
                    settings.HourlyRate * hours, percent);
            }
            else
            {
                continue; // Kept as it is: costs nothing and says nothing about the price's confidence.
            }

            if (measured && ConfidenceOf(source) < confidence)
            {
                confidence = ConfidenceOf(source);
            }
        }

        var priced = lines.ToList();
        var fixedLines = FixedLines(priced, settings);
        IReadOnlyList<PriceLine> all = [.. priced, .. fixedLines];

        var subtotals = Enum.GetValues<LineKind>().ToDictionary(
            kind => kind,
            kind => all.Where(l => l.Kind == kind).Select(l => l.Amount).Aggregate(PriceRange.Zero, PriceRange.Add));
        var sum = subtotals.Values.Aggregate(PriceRange.Zero, PriceRange.Add);

        // Rounded once, outwards, to whole hundreds: "42 317 – 51 042 kr" would look more exact than it is.
        var total = new PriceRange(
            Money.InSek(Math.Floor(sum.Low.Amount / 100m) * 100m),
            Money.InSek(Math.Ceiling(sum.High.Amount / 100m) * 100m));
        var vatShare = settings.VatPercent / (100m + settings.VatPercent);
        var vat = new PriceRange((total.Low * vatShare).RoundToWhole(), (total.High * vatShare).RoundToWhole());

        return new PriceEstimate(all, subtotals, total, vat, confidence, missing);
    }

    /// <summary>Square meters for surfaces, meters for lines, one for points; null for what is never priced.</summary>
    private static decimal? Quantity(SceneElement element) => element switch
    {
        SurfaceElement surface => (decimal)new Polygon(surface.Points).Area.SquareMeters,
        LineElement line => (decimal)new Polyline(line.Points).Length.Meters,
        PointElement => 1m,
        _ => null,
    };

    /// <summary>A sloped plot makes areas and lengths one step less certain (docs/design.md, "Scendokumentet").</summary>
    private static MeasurementSource Effective(MeasurementSource source, bool sloped) => !sloped
        ? source
        : source switch
        {
            MeasurementSource.Verified => MeasurementSource.Customer,
            _ => MeasurementSource.Estimated,
        };

    private static Confidence ConfidenceOf(MeasurementSource source) => source switch
    {
        MeasurementSource.Estimated => Confidence.Low,
        MeasurementSource.Customer => Confidence.Medium,
        _ => Confidence.High,
    };

    /// <summary>
    /// The element's article when it exists and fits the element: a point needs an article sold per piece, a line per
    /// meter and a surface per square meter. The browser is not trusted to send matching ids.
    /// </summary>
    private static ArticlePriceFacts? UsableArticle(SceneElement element, IReadOnlyDictionary<Guid, ArticlePriceFacts> articles)
    {
        var (articleId, unit) = element switch
        {
            SurfaceElement surface => (surface.ArticleId, "square-meter"),
            LineElement line => (line.ArticleId, "meter"),
            PointElement point => (point.ArticleId, Piece),
            _ => (null, string.Empty),
        };

        return articleId is { } id && articles.TryGetValue(id, out var article) && article.Unit == unit ? article : null;
    }

    /// <summary>
    /// Surfaces by their material. Points and lines by their article's type model; without one, a point counts as a
    /// deciduous tree (the most work, so the price is never silently too low) and a line as a hedge.
    /// </summary>
    private static RemovalClass ClassOf(SceneElement element, ArticlePriceFacts? article) => element switch
    {
        SurfaceElement surface => surface.Material switch
        {
            SurfaceMaterial.Grass => RemovalClass.Grass,
            SurfaceMaterial.Paving => RemovalClass.Paving,
            SurfaceMaterial.Gravel => RemovalClass.Gravel,
            SurfaceMaterial.Decking => RemovalClass.Decking,
            _ => RemovalClass.Bed,
        },
        LineElement => article?.TypeModel == "fence" ? RemovalClass.Fence : RemovalClass.Hedge,
        _ => article?.TypeModel switch
        {
            null or "deciduous-tree" => RemovalClass.DeciduousTree,
            "conifer" => RemovalClass.Conifer,
            _ => RemovalClass.ShrubOrOther,
        },
    };

    /// <summary>Setting up and travelling whenever there is work; carting away whenever something is removed.</summary>
    private static List<PriceLine> FixedLines(List<PriceLine> priced, PriceSettings settings)
    {
        var result = new List<PriceLine>();
        if (priced.Count == 0)
        {
            return result;
        }

        result.Add(Fixed("Etablering", settings.Establishment));
        result.Add(Fixed("Transport", settings.Transport));
        if (priced.Any(l => l.Kind == LineKind.Removal))
        {
            result.Add(Fixed("Bortforsling", settings.Disposal));
        }

        return result;
    }

    private static PriceLine Fixed(string label, Money amount) => new(LineKind.Fixed, label, 1m, Piece, new PriceRange(amount, amount));

    /// <summary>Merges elements into one line per kind and key (article or removal class), in the order first seen.</summary>
    private sealed class LineCollector
    {
        private readonly Dictionary<(LineKind Kind, string Key), int> _index = [];
        private readonly List<PriceLine> _lines = [];

        public void Add(LineKind kind, string key, string label, string unit, decimal quantity, Money amount, decimal percent)
        {
            var range = new PriceRange(amount * (1m - (percent / 100m)), amount * (1m + (percent / 100m)));
            if (_index.TryGetValue((kind, key), out var position))
            {
                var line = _lines[position];
                _lines[position] = line with { Quantity = line.Quantity + quantity, Amount = line.Amount + range };
            }
            else
            {
                _index[(kind, key)] = _lines.Count;
                _lines.Add(new PriceLine(kind, label, quantity, unit, range));
            }
        }

        /// <summary>Material, labor, removal, each in the order first seen. Lines worth nothing (no labor hours yet) are left out.</summary>
        public List<PriceLine> ToList() => [.. _lines.Where(l => l.Amount.High.Amount != 0).OrderBy(l => l.Kind)];
    }
}
