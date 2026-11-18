using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Pricing.Domain;
using GardenBuilder.Modules.Pricing.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Pricing.Features.EstimatePrice;

/// <summary>
/// The editor's live price: the browser sends the scene, the server answers with an approximate range. The browser is
/// never trusted with prices, so it sends only the scene and article ids (docs/design.md, "Prissättning och offertflöde").
/// </summary>
internal static class EstimatePrice
{
    /// <summary>A scene is a few kilobytes; one megabyte leaves plenty of room and stops abuse.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    public sealed record RangeResponse(decimal Low, decimal High);

    /// <param name="Kind">"material", "labor", "removal" or "fixed".</param>
    /// <param name="Unit">"piece", "meter", "square-meter" or "hour".</param>
    public sealed record LineResponse(string Kind, string Label, decimal Quantity, string Unit, decimal Low, decimal High);

    public sealed record SubtotalsResponse(RangeResponse Material, RangeResponse Labor, RangeResponse Removal, RangeResponse Fixed);

    /// <param name="Confidence">"low", "medium" or "high".</param>
    public sealed record EstimateResponse(
        IReadOnlyList<LineResponse> Lines,
        SubtotalsResponse Subtotals,
        RangeResponse Total,
        RangeResponse Vat,
        string Confidence,
        int MissingArticles);

    public sealed record ProblemResponse(string Code, Guid? ElementId);

    /// <param name="Code">"unreadable-scene" or "invalid-scene".</param>
    /// <param name="Problems">What is wrong with an invalid scene; left out for an unreadable one.</param>
    public sealed record ErrorResponse(
        string Code,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProblemResponse>? Problems = null);

    public static RouteGroupBuilder MapEstimatePrice(this RouteGroupBuilder group)
    {
        group.MapPost("/estimate", HandleAsync)
            .WithName("EstimatePrice")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));
        return group;
    }

    private static async Task<Results<Ok<EstimateResponse>, BadRequest<ErrorResponse>, StatusCodeHttpResult>> HandleAsync(
        HttpRequest request,
        ISceneCheck sceneCheck,
        ICatalogPrices catalogPrices,
        PriceSettingsStore settingsStore,
        CancellationToken cancellationToken)
    {
        // Read as text and parsed by SceneJson, the one reader every scene goes through, so its rules apply here too.
        if (await ReadBodyAsync(request, cancellationToken) is not { } json)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        SceneDocument scene;
        try
        {
            scene = SceneJson.Deserialize(json);
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest(new ErrorResponse("unreadable-scene"));
        }

        var problems = sceneCheck.Check(scene);
        if (problems.Count > 0)
        {
            return TypedResults.BadRequest(new ErrorResponse("invalid-scene", [.. problems.Select(p => new ProblemResponse(p.Code, p.ElementId))]));
        }

        var facts = await catalogPrices.GetAsync(ArticleIds(scene), cancellationToken);
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var estimate = PriceCalculator.Estimate(scene, facts.ToDictionary(f => f.Id), settings);

        return TypedResults.Ok(ToResponse(estimate));
    }

    /// <summary>
    /// The body as text, or null when it is larger than <see cref="MaxBodyBytes"/>. Kestrel enforces the limit from the
    /// endpoint metadata; counting here as well keeps the rule when a server does not (the test server).
    /// </summary>
    private static async Task<string?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        try
        {
            int read;
            while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null;
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>Every article the scene mentions, whatever the element's status: removed things are priced by their type.</summary>
    internal static List<Guid> ArticleIds(SceneDocument scene) =>
    [
        .. scene.Elements
            .Select(element => element switch
            {
                SurfaceElement surface => surface.ArticleId,
                LineElement line => line.ArticleId,
                PointElement point => point.ArticleId,
                _ => null,
            })
            .OfType<Guid>()
            .Distinct(),
    ];

    private static EstimateResponse ToResponse(PriceEstimate estimate) => new(
        [.. estimate.Lines.Select(l => new LineResponse(Key(l.Kind), l.Label, l.Quantity, l.Unit, l.Amount.Low.Amount, l.Amount.High.Amount))],
        new SubtotalsResponse(
            Range(estimate.Subtotals[LineKind.Material]),
            Range(estimate.Subtotals[LineKind.Labor]),
            Range(estimate.Subtotals[LineKind.Removal]),
            Range(estimate.Subtotals[LineKind.Fixed])),
        Range(estimate.Total),
        Range(estimate.Vat),
        estimate.Confidence.ToString().ToLowerInvariant(),
        estimate.MissingArticles);

    private static string Key(LineKind kind) => kind.ToString().ToLowerInvariant();

    private static RangeResponse Range(PriceRange range) => new(range.Low.Amount, range.High.Amount);
}
