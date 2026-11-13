using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GardenBuilder.Contracts.Scenes;

/// <summary>
/// The one way to turn a scene into JSON and back. Reading is strict: unknown properties, unknown element kinds and
/// missing required properties are rejected, so half-understood input (for example from an AI) never becomes a scene.
/// </summary>
public static class SceneJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(SceneDocument scene) => JsonSerializer.Serialize(scene, Options);

    /// <summary>Reads a scene. Throws <see cref="JsonException"/> for any malformed input.</summary>
    public static SceneDocument Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SceneDocument>(json, Options)
                ?? throw new JsonException("A scene document cannot be null.");
        }
        catch (NotSupportedException exception)
        {
            // System.Text.Json reports some malformed polymorphic input this way; callers handle one exception type.
            throw new JsonException(exception.Message, exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.MakeReadOnly();
        return options;
    }
}
