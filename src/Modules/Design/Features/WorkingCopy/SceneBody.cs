using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GardenBuilder.Contracts.Scenes;
using Microsoft.AspNetCore.Http;

namespace GardenBuilder.Modules.Design.Features.WorkingCopy;

/// <summary>
/// Reads a scene from a request body the same way the price estimate does: at most one megabyte, read by
/// <see cref="SceneJson"/> and checked by <see cref="ISceneCheck"/>. A scene that fails here is never stored.
/// </summary>
internal static class SceneBody
{
    /// <summary>A scene is a few kilobytes; one megabyte leaves plenty of room and stops abuse.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    public sealed record ProblemResponse(string Code, Guid? ElementId);

    /// <param name="Code">"unreadable-scene" or "invalid-scene".</param>
    /// <param name="Problems">What is wrong with an invalid scene; left out for an unreadable one.</param>
    public sealed record ErrorResponse(
        string Code,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProblemResponse>? Problems = null);

    /// <summary>The checked scene, or the error to answer with (413 or 400).</summary>
    public static async Task<(SceneDocument? Scene, IResult? Error)> ReadAsync(
        HttpRequest request, ISceneCheck sceneCheck, CancellationToken cancellationToken)
    {
        if (await ReadTextAsync(request, cancellationToken) is not { } json)
        {
            return (null, TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge));
        }

        SceneDocument scene;
        try
        {
            scene = SceneJson.Deserialize(json);
        }
        catch (JsonException)
        {
            return (null, TypedResults.BadRequest(new ErrorResponse("unreadable-scene")));
        }

        var problems = sceneCheck.Check(scene);
        if (problems.Count > 0)
        {
            return (null, TypedResults.BadRequest(
                new ErrorResponse("invalid-scene", [.. problems.Select(p => new ProblemResponse(p.Code, p.ElementId))])));
        }

        return (scene, null);
    }

    /// <summary>
    /// The body as text, or null when it is larger than <see cref="MaxBodyBytes"/>. Kestrel enforces the limit from the
    /// endpoint metadata; counting here as well keeps the rule when a server does not (the test server).
    /// </summary>
    private static async Task<string?> ReadTextAsync(HttpRequest request, CancellationToken cancellationToken)
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
}
