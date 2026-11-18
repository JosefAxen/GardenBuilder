using System.Text.Json;

namespace GardenBuilder.Modules.Ai.Features.PhotoInterpretation;

/// <summary>
/// The question Claude gets with each photo, and the shape its answer must have. Versioned: change the text or the
/// schema, raise <see cref="Version"/>, and rerun the photo trial (evals/README.md) before it reaches customers.
/// </summary>
internal static class PhotoPrompt
{
    public const string Version = "photo-interpretation@1";

    public const int MaxTokens = 16000;

    public const string Text = """
        You look at one photo of a private garden in Sweden. Describe what is in it as a rough map seen from above, so a
        3D garden editor can recreate the garden. Only include things you can actually see.

        Coordinates are meters seen from above: the camera stands at (0, 0), y points straight ahead (the direction the
        camera looks) and x points to the right. Estimate sizes from familiar things: a door is about 2.0 m high, a
        window about 1.2 m, a fence about 1.2 m, a paving slab 0.4 to 0.6 m.

        For each thing give:
        - type: lawn, flowerbed, gravel, patio, path, hedge, fence, tree, shrub, raised-bed, shed, edging or other
        - label: a short Swedish name, e.g. "Gräsmatta", "Tujahäck", "Björk"
        - shape: area (lawn, flowerbed, gravel, patio, path: 3 to 8 corners in order), line (hedge, fence, edging:
          2 or more points along it) or point (tree, shrub: one point at its centre)
        - widthM: crown or plant width for points, thickness for lines, otherwise null
        - heightM: height in meters, or null when you cannot tell
        - confidence: 0.0 to 1.0, how sure you are that the thing is there and roughly where you put it

        For the house: whether it is visible, its wall colour in plain English (e.g. "falu red", "yellow", "white",
        "grey"), its roof form (gable, hip, flat, shed or unknown), its roof colour, and how sure you are.
        Put what you could not tell in "unsure", in Swedish.
        """;

    /// <summary>Structured output: Claude's answer must be JSON of exactly this shape.</summary>
    public static JsonElement Schema { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["summary", "house", "elements", "unsure"],
          "properties": {
            "summary": { "type": "string" },
            "house": {
              "type": "object",
              "additionalProperties": false,
              "required": ["visible", "wallColor", "roofForm", "roofColor", "confidence"],
              "properties": {
                "visible": { "type": "boolean" },
                "wallColor": { "type": "string" },
                "roofForm": { "type": "string", "enum": ["gable", "hip", "flat", "shed", "unknown"] },
                "roofColor": { "type": "string" },
                "confidence": { "type": "number" }
              }
            },
            "elements": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["type", "label", "shape", "points", "widthM", "heightM", "confidence"],
                "properties": {
                  "type": {
                    "type": "string",
                    "enum": ["lawn", "flowerbed", "gravel", "patio", "path", "hedge", "fence", "tree", "shrub", "raised-bed", "shed", "edging", "other"]
                  },
                  "label": { "type": "string" },
                  "shape": { "type": "string", "enum": ["area", "line", "point"] },
                  "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } },
                  "widthM": { "anyOf": [{ "type": "number" }, { "type": "null" }] },
                  "heightM": { "anyOf": [{ "type": "number" }, { "type": "null" }] },
                  "confidence": { "type": "number" }
                }
              }
            },
            "unsure": { "type": "array", "items": { "type": "string" } }
          }
        }
        """).RootElement.Clone();
}
