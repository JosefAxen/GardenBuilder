using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using GardenBuilder.SharedKernel;

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

    /// <summary>
    /// The JSON schema for <see cref="SceneDocument"/>. Every named scene type is described once under "$defs" and
    /// referenced from where it is used, so the generated TypeScript gets one named type per C# type.
    /// </summary>
    public static JsonNode ExportSchema()
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            // Generic arguments such as IReadOnlyList<SceneElement> carry no nullability; none of them may be null.
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                var type = context.TypeInfo.Type;
                if (node is not JsonObject schema || !IsSceneType(type))
                {
                    return node;
                }

                if (type == typeof(SceneElement))
                {
                    // The base is only the union of the element types; "type"/"required" here would intersect with it.
                    schema.Remove("type");
                    schema.Remove("required");
                }
                else if (type.IsSubclassOf(typeof(SceneElement)) && schema["required"] is JsonArray required
                    && !required.Any(n => n?.GetValue<string>() == "kind"))
                {
                    // "kind" is what tells the element types apart, so every element must carry it.
                    required.Insert(0, "kind");
                }

                if (!schema.ContainsKey("title"))
                {
                    schema.Insert(0, "title", type.Name);
                }

                return node;
            },
        };

        var root = Options.GetJsonSchemaAsNode(typeof(SceneDocument), exporterOptions).AsObject();
        var defs = new JsonObject();
        HoistNamedTypes(root, defs);
        root["$defs"] = defs;
        return root;
    }

    private static bool IsSceneType(Type type) =>
        type == typeof(Point2) || type.Namespace == typeof(SceneDocument).Namespace;

    /// <summary>Replaces every titled schema below <paramref name="node"/> with a "$ref" into <paramref name="defs"/>.</summary>
    private static void HoistNamedTypes(JsonNode node, JsonObject defs)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (obj[key] is { } child && Hoist(child, defs) is var lifted && !ReferenceEquals(lifted, child))
                    {
                        obj[key] = lifted;
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is { } child && Hoist(child, defs) is var lifted && !ReferenceEquals(lifted, child))
                    {
                        array[i] = lifted;
                    }
                }

                break;
        }
    }

    private static JsonNode Hoist(JsonNode child, JsonObject defs)
    {
        if (child is JsonObject schema && schema["title"] is JsonValue title && title.TryGetValue<string>(out var name))
        {
            if (!defs.ContainsKey(name))
            {
                HoistNamedTypes(schema, defs);
                defs[name] = schema.DeepClone();
            }

            return new JsonObject { ["$ref"] = "#/$defs/" + name };
        }

        HoistNamedTypes(child, defs);
        return child;
    }

    /// <summary>
    /// Point2 is a struct, so System.Text.Json builds it with the default constructor and a missing "y" would silently
    /// become 0. Marking both coordinates required makes reading reject it and the schema say so.
    /// </summary>
    private static void RequireAllPoint2Coordinates(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Point2))
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            property.IsRequired = true;
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
            // The web defaults accept numbers written as strings ("5"); a scene must use real numbers.
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RequireAllPoint2Coordinates } },
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.MakeReadOnly();
        return options;
    }
}
