using System.Globalization;
using System.Text.Json;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Contracts.Scenes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Design.Features.WorkingCopy;

/// <summary>
/// The editor's saving: fetch the latest design, create one on the first change, and save the working copy with the
/// revision it was based on (optimistic locking, docs/superpowers/specs/2026-11-16-garden-design-design.md).
/// A design that belongs to someone else answers exactly like one that does not exist.
/// </summary>
internal static class DesignEndpoints
{
    /// <param name="GardenId">The garden the design belongs to, so the client can reach its photos.</param>
    public sealed record DesignResponse(Guid Id, Guid GardenId, int Revision, JsonElement Scene);

    public sealed record SavedResponse(Guid Id, int Revision, JsonElement Scene);

    public sealed record ConflictResponse(string Code, int Revision);

    public sealed record CodeResponse(string Code);

    public sealed record ExistsResponse(string Code, Guid Id, int Revision);

    public static RouteGroupBuilder MapWorkingCopy(this RouteGroupBuilder group)
    {
        group.MapGet("/latest", GetLatestAsync).WithName("GetLatestDesign");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetDesign");
        group.MapPost("/", CreateAsync)
            .WithName("CreateDesign")
            .WithMetadata(new RequestSizeLimitAttribute(SceneBody.MaxBodyBytes));
        group.MapPut("/{id:guid}/working-copy", SaveAsync)
            .WithName("SaveWorkingCopy")
            .WithMetadata(new RequestSizeLimitAttribute(SceneBody.MaxBodyBytes));
        return group;
    }

    /// <summary>The customer's most recently saved design. Never creates a customer: a visitor who only looks leaves nothing behind.</summary>
    private static async Task<IResult> GetLatestAsync(
        HttpResponse response, ICurrentCustomer customer, WorkingCopyStore store, CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId
            || await store.LatestAsync(customerId, cancellationToken) is not { } design)
        {
            return TypedResults.NotFound();
        }

        SetRevision(response, design.Revision);
        return TypedResults.Ok(new DesignResponse(design.Id, design.GardenId, design.Revision, Upgraded(design.WorkingCopy)));
    }

    private static async Task<IResult> GetAsync(
        Guid id, HttpResponse response, ICurrentCustomer customer, WorkingCopyStore store, CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId
            || await store.FindAsync(id, customerId, cancellationToken) is not { } design)
        {
            return TypedResults.NotFound();
        }

        SetRevision(response, design.Revision);
        return TypedResults.Ok(new DesignResponse(design.Id, design.GardenId, design.Revision, Upgraded(design.WorkingCopy)));
    }

    /// <summary>
    /// The first save. The scene is checked before anything is created, so a bad scene leaves no customer or garden behind.
    /// A customer has one design for now: when there already is one (another tab got there first, or the answer to an
    /// earlier POST was lost) this answers 409 with it instead of creating a second.
    /// </summary>
    private static async Task<IResult> CreateAsync(
        HttpRequest request,
        HttpResponse response,
        ISceneCheck sceneCheck,
        ICurrentCustomer customer,
        IGardens gardens,
        WorkingCopyStore store,
        CancellationToken cancellationToken)
    {
        var (scene, error) = await SceneBody.ReadAsync(request, sceneCheck, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await customer.FindAsync(cancellationToken) is { } knownCustomer
            && await store.LatestAsync(knownCustomer, cancellationToken) is { } existing)
        {
            return TypedResults.Json(
                new ExistsResponse("design-exists", existing.Id, existing.Revision), statusCode: StatusCodes.Status409Conflict);
        }

        var customerId = await customer.GetOrCreateAsync(cancellationToken);
        var gardenId = await gardens.CreateAsync(customerId, cancellationToken);
        var design = await store.CreateAsync(gardenId, customerId, scene!, cancellationToken);

        SetRevision(response, design.Revision);
        return TypedResults.Created(
            $"/api/designs/{design.Id}", new DesignResponse(design.Id, design.GardenId, design.Revision, Upgraded(design.WorkingCopy)));
    }

    /// <summary>Saves only when If-Match names the revision the design has now; otherwise 412 with the current revision.</summary>
    private static async Task<IResult> SaveAsync(
        Guid id,
        HttpRequest request,
        HttpResponse response,
        ISceneCheck sceneCheck,
        ICurrentCustomer customer,
        WorkingCopyStore store,
        CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId)
        {
            return TypedResults.NotFound();
        }

        if (ExpectedRevision(request) is not { } expected)
        {
            return TypedResults.Json(new CodeResponse("revision-required"), statusCode: StatusCodes.Status428PreconditionRequired);
        }

        var (scene, error) = await SceneBody.ReadAsync(request, sceneCheck, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        switch (await store.SaveAsync(id, customerId, expected, scene!, cancellationToken))
        {
            case SaveResult.Saved saved:
                SetRevision(response, saved.Revision);
                return TypedResults.Ok(new SavedResponse(id, saved.Revision, JsonSerializer.SerializeToElement(scene, SceneJson.Options)));
            case SaveResult.Conflict conflict:
                return TypedResults.Json(
                    new ConflictResponse("conflict", conflict.CurrentRevision), statusCode: StatusCodes.Status412PreconditionFailed);
            default:
                return TypedResults.NotFound();
        }
    }

    /// <summary>The revision in If-Match: "3", W/"3" or 3. Null when it is missing, a list, "*" or not a positive number.</summary>
    private static int? ExpectedRevision(HttpRequest request)
    {
        var values = request.Headers.IfMatch;
        if (values.Count != 1 || values[0] is not { } raw)
        {
            return null;
        }

        var text = raw.Trim();
        if (text.StartsWith("W/", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        text = text.Trim('"');
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) && revision > 0
            ? revision
            : null;
    }

    private static void SetRevision(HttpResponse response, int revision) =>
        response.Headers.ETag = $"\"{revision.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>The stored scene in the current format: an older version is upgraded on the way out, not in the database.</summary>
    private static JsonElement Upgraded(string json) =>
        JsonSerializer.SerializeToElement(SceneJson.Deserialize(json), SceneJson.Options);
}
