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
        EnsureSupportedFormatVersion(json);

        SceneDocument scene;
        try
        {
            scene = JsonSerializer.Deserialize<SceneDocument>(json, Options)
                ?? throw new JsonException("A scene document cannot be null.");
        }
        catch (NotSupportedException exception)
        {
            // System.Text.Json reports some malformed polymorphic input this way; callers handle one exception type.
            throw new JsonException(exception.Message, exception);
        }

        // Nullability of generic arguments is not enforced while reading, so [null] would otherwise get through.
        if (scene.Elements.Any(element => element is null))
        {
            throw new JsonException("A scene element cannot be null.");
        }

        // A number like 1e400 overflows to Infinity, which nothing downstream can use and Serialize refuses to write.
        if (!scene.Elements.All(HasOnlyFiniteNumbers))
        {
            throw new JsonException("A scene can only contain finite numbers.");
        }

        // Version 1 lacks only optional fields, so upgrading it means calling it version 2.
        return scene.FormatVersion < SceneDocument.CurrentFormatVersion
            ? scene with { FormatVersion = SceneDocument.CurrentFormatVersion }
            : scene;
    }

    private static bool HasOnlyFiniteNumbers(SceneElement element) => element switch
    {
        PlotElement plot => plot.Points.All(IsFinite),
        BuildingElement building => building.Points.All(IsFinite) && double.IsFinite(building.Height),
        SurfaceElement surface => surface.Points.All(IsFinite),
        LineElement line => line.Points.All(IsFinite) && IsFinite(line.Height),
        PointElement point => IsFinite(point.Position) && double.IsFinite(point.Rotation) && IsFinite(point.Width) && IsFinite(point.Height),
        _ => true,
    };

    private static bool IsFinite(Point2 point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool IsFinite(double? value) => value is not { } number || double.IsFinite(number);

    /// <summary>
    /// Reads only "formatVersion" first, so a document from another format version is reported as exactly that,
    /// instead of failing on whatever new property that version happens to have.
    /// </summary>
    private static void EnsureSupportedFormatVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("formatVersion", out var version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out var number)
            && (number < SceneDocument.OldestReadableFormatVersion || number > SceneDocument.CurrentFormatVersion))
        {
            throw new JsonException(
                $"Unsupported scene format version {number}; this version reads format versions {SceneDocument.OldestReadableFormatVersion} to {SceneDocument.CurrentFormatVersion}.");
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
    internal static void HoistNamedTypes(JsonNode node, JsonObject defs)
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
            var reference = new JsonObject { ["$ref"] = "#/$defs/" + name };

            // The exporter writes a repeated type as a "$ref" to its first use; that is the same type, not a new shape.
            if (schema.ContainsKey("$ref"))
            {
                return reference;
            }

            HoistNamedTypes(schema, defs);
            if (defs[name] is { } existing)
            {
                // "$defs" is keyed on the type name; two different shapes under one name would silently merge.
                if (!JsonNode.DeepEquals(existing, schema))
                {
                    throw new InvalidOperationException(
                        $"Two different schemas are both named '{name}'. Rename one of the types or describe them alike. "
                        + $"First: {existing.ToJsonString()} Second: {schema.ToJsonString()}");
                }
            }
            else
            {
                defs[name] = schema.DeepClone();
            }

            return reference;
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
            // Otherwise {"x":1,"x":999} silently reads as 999.
            AllowDuplicateProperties = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RequireAllPoint2Coordinates } },
        };
        // Enums only as their names: "status": 5 means nothing and must not slip past the rules.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }
}
