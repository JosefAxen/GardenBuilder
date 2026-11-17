using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Features.ImportArticles;

/// <summary>
/// Made-up articles to fill the catalog until the company's real list arrives. Written to
/// assets/catalog/artiklar-exempel.xlsx by the import tool. Thujas come in several sizes, as a nursery sells them.
/// </summary>
internal static class ExampleCatalog
{
    private const Light SunToPartShade = Light.Sun | Light.PartShade;

    public static IReadOnlyList<ArticleData> Articles { get; } =
    [
        // Trees
        Plant("TR-AP-150", "Äppelträd Rubinola 150–175 cm", "Äppelträd Rubinola", "Träd", TypeModel.TreeFruit, 895m, zone: 4, height: 1.6, matureHeight: 4, matureWidth: 4, growth: 30, Light.Sun, "#5f9e3f"),
        Plant("TR-BJ-200", "Hängbjörk 200–250 cm", "Hängbjörk", "Träd", TypeModel.TreeLarge, 1195m, zone: 6, height: 2.2, matureHeight: 18, matureWidth: 8, growth: 50, SunToPartShade, "#8dbb4c"),
        Plant("TR-LO-060", "Japansk lönn 60–80 cm", "Japansk lönn", "Träd", TypeModel.TreeSmall, 695m, zone: 3, height: 0.7, matureHeight: 4, matureWidth: 3, growth: 20, Light.PartShade, "#b5452f"),
        Plant("TR-KO-150", "Körsbärsträd Stella 150–175 cm", "Körsbärsträd Stella", "Träd", TypeModel.TreeFruit, 945m, zone: 3, height: 1.6, matureHeight: 5, matureWidth: 4, growth: 35, Light.Sun, "#6a9a3f"),
        Plant("BA-SM-040", "Tuja Smaragd 40–60 cm", "Tuja Smaragd", "Barrträd", TypeModel.ConiferColumn, 189m, zone: 5, height: 0.5, matureHeight: 5, matureWidth: 1.2, growth: 20, SunToPartShade, "#3f7a3a"),
        Plant("BA-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Barrträd", TypeModel.ConiferColumn, 395m, zone: 5, height: 0.9, matureHeight: 5, matureWidth: 1.2, growth: 20, SunToPartShade, "#3f7a3a"),
        Plant("BA-SM-120", "Tuja Smaragd 120–140 cm", "Tuja Smaragd", "Barrträd", TypeModel.ConiferColumn, 695m, zone: 5, height: 1.3, matureHeight: 5, matureWidth: 1.2, growth: 20, SunToPartShade, "#3f7a3a"),
        Plant("BA-BR-060", "Tuja Brabant 60–80 cm", "Tuja Brabant", "Barrträd", TypeModel.ConiferColumn, 159m, zone: 5, height: 0.7, matureHeight: 8, matureWidth: 2, growth: 35, SunToPartShade, "#4c8a3e"),
        Plant("BA-BR-100", "Tuja Brabant 100–120 cm", "Tuja Brabant", "Barrträd", TypeModel.ConiferColumn, 329m, zone: 5, height: 1.1, matureHeight: 8, matureWidth: 2, growth: 35, SunToPartShade, "#4c8a3e"),
        Plant("BA-GR-100", "Serbisk gran 100–125 cm", "Serbisk gran", "Barrträd", TypeModel.Spruce, 749m, zone: 6, height: 1.1, matureHeight: 20, matureWidth: 3, growth: 30, SunToPartShade, "#2f5d3a"),
        Plant("BA-TL-150", "Tall 150–175 cm", "Tall", "Barrträd", TypeModel.Pine, 595m, zone: 7, height: 1.6, matureHeight: 20, matureWidth: 6, growth: 30, Light.Sun, "#3d6e3a"),
        Plant("BA-TA-030", "Bergtall Mops 30–40 cm", "Bergtall Mops", "Barrträd", TypeModel.ShrubRound, 349m, zone: 7, height: 0.35, matureHeight: 1, matureWidth: 1, growth: 5, Light.Sun, "#356b35"),

        // Shrubs
        Plant("BU-SY-060", "Syren 60–80 cm", "Syren", "Buskar", TypeModel.ShrubFlowering, 299m, zone: 6, height: 0.7, matureHeight: 4, matureWidth: 2.5, growth: 30, Light.Sun, "#6a9a4a", "#a77bca"),
        Plant("BU-HO-040", "Hortensia Annabelle 40–60 cm", "Hortensia Annabelle", "Buskar", TypeModel.ShrubFlowering, 249m, zone: 5, height: 0.5, matureHeight: 1.5, matureWidth: 1.5, growth: 25, Light.PartShade, "#5f8f45", "#f2f0e1"),
        Plant("BU-BX-030", "Buxbom klot 30 cm", "Buxbom klot", "Buskar", TypeModel.ShrubRound, 279m, zone: 4, height: 0.3, matureHeight: 1, matureWidth: 0.6, growth: 5, SunToPartShade | Light.Shade, "#3d6b35"),
        Plant("BU-RH-040", "Rhododendron Cunningham's White 40–50 cm", "Rhododendron Cunningham's White", "Buskar", TypeModel.ShrubFlowering, 329m, zone: 5, height: 0.45, matureHeight: 2, matureWidth: 2, growth: 10, Light.PartShade, "#3f6e3a", "#f5f2f7"),
        Plant("BU-SP-030", "Spirea Goldflame 30–40 cm", "Spirea Goldflame", "Buskar", TypeModel.ShrubFlowering, 119m, zone: 6, height: 0.35, matureHeight: 0.8, matureWidth: 1, growth: 15, Light.Sun, "#9fbf45", "#e86a9a"),

        // Perennials
        Plant("PE-LA-001", "Lavendel Hidcote, kruka 2 l", "Lavendel Hidcote", "Perenner", TypeModel.PerennialTall, 79m, zone: 4, height: 0.25, matureHeight: 0.5, matureWidth: 0.5, growth: 15, Light.Sun, "#7c9a6a", "#6f5bb5"),
        Plant("PE-FU-001", "Funkia Francee, kruka 2 l", "Funkia Francee", "Perenner", TypeModel.FoliagePlant, 89m, zone: 6, height: 0.3, matureHeight: 0.6, matureWidth: 0.7, growth: 10, Light.PartShade | Light.Shade, "#4d8a4a", "#e8e3c8"),
        Plant("PE-RU-001", "Rudbeckia Goldsturm, kruka 2 l", "Rudbeckia Goldsturm", "Perenner", TypeModel.PerennialTall, 69m, zone: 5, height: 0.4, matureHeight: 0.7, matureWidth: 0.5, growth: 20, Light.Sun, "#55803d", "#f2b632"),
        Plant("PE-PI-001", "Pion Sarah Bernhardt, kruka 3 l", "Pion Sarah Bernhardt", "Perenner", TypeModel.PerennialTall, 199m, zone: 5, height: 0.4, matureHeight: 0.9, matureWidth: 0.9, growth: 15, Light.Sun, "#4f7d3f", "#f2a7c3"),
        Plant("PE-VI-001", "Vintergröna, kruka 1 l", "Vintergröna", "Perenner", TypeModel.PerennialLow, 49m, zone: 5, height: 0.1, matureHeight: 0.15, matureWidth: 0.5, growth: 15, Light.PartShade | Light.Shade, "#3f6e3a", "#7a7ad8"),
        Plant("PE-FJ-001", "Fjädergräs Karl Foerster, kruka 2 l", "Fjädergräs Karl Foerster", "Perenner", TypeModel.OrnamentalGrass, 99m, zone: 6, height: 0.4, matureHeight: 1.5, matureWidth: 0.6, growth: 30, Light.Sun, "#a3a35a"),

        // Hedges, sold per meter
        Hedge("HA-SM-060", "Thujahäck Smaragd 60–80 cm", "Tuja Smaragd", 299m, zone: 5, height: 0.7, width: 0.5, clipHeight: 1.5, growth: 20, "#3f7a3a"),
        Hedge("HA-BR-100", "Thujahäck Brabant 100–120 cm", "Tuja Brabant", 389m, zone: 5, height: 1.1, width: 0.6, clipHeight: 1.8, growth: 35, "#4c8a3e"),
        Hedge("HA-LI-060", "Ligusterhäck 60–80 cm", "Liguster", 189m, zone: 4, height: 0.7, width: 0.4, clipHeight: 1.2, growth: 40, "#3f7a3a"),
        Hedge("HA-BO-080", "Bokhäck 80–100 cm", "Bok", 249m, zone: 4, height: 0.9, width: 0.4, clipHeight: 1.6, growth: 40, "#6f8f3a"),

        // Fences, sold per meter
        Thing("ST-SP-100", "Spjälstaket 100 cm", "Häckar och staket", ArticleUnit.Meter, TypeModel.Fence, 549m, "#d9cdb6", height: 1.0, width: 0.08),
        Thing("ST-PL-180", "Plank 180 cm", "Häckar och staket", ArticleUnit.Meter, TypeModel.Fence, 1195m, "#a88b6a", height: 1.8, width: 0.1),

        // Pots and furniture
        Thing("KR-TE-040", "Terrakottakruka 40 cm", "Krukor och möbler", ArticleUnit.Piece, TypeModel.Pot, 349m, "#c06a3f", accent: "#5b8f45"),
        Thing("KR-TE-060", "Terrakottakruka 60 cm", "Krukor och möbler", ArticleUnit.Piece, TypeModel.Pot, 649m, "#c06a3f", accent: "#5b8f45", scale: 1.5),
        Thing("ST-GR-040", "Dekorsten granit 40 cm", "Krukor och möbler", ArticleUnit.Piece, TypeModel.Rock, 495m, "#8f8c86"),
        Thing("MO-BA-150", "Trädgårdsbänk ek 150 cm", "Krukor och möbler", ArticleUnit.Piece, TypeModel.Furniture, 3495m, "#8a6a4a"),
        Thing("MO-BE-180", "Sittbänk betong 180 cm", "Krukor och möbler", ArticleUnit.Piece, TypeModel.Furniture, 5995m, "#b8b3aa", scale: 1.2),

        // Surfaces, sold per square meter
        Thing("YT-GR-001", "Gräsmatta, rullgräs", "Ytor", ArticleUnit.SquareMeter, TypeModel.SurfaceGrass, 89m, "#7fb069"),
        Thing("YT-PL-040", "Betongplattor 40 × 40 cm", "Ytor", ArticleUnit.SquareMeter, TypeModel.SurfacePaving, 395m, "#c9c2b5"),
        Thing("YT-GU-816", "Grus 8–16 mm", "Ytor", ArticleUnit.SquareMeter, TypeModel.SurfaceGravel, 129m, "#d8d0bf"),
        Thing("YT-TD-001", "Trädäck, tryckimpregnerat", "Ytor", ArticleUnit.SquareMeter, TypeModel.SurfaceDecking, 1295m, "#a77b52"),
        Thing("YT-RA-001", "Rabatt, planteringsjord", "Ytor", ArticleUnit.SquareMeter, TypeModel.SurfaceBed, 149m, "#6b4f3a"),
    ];

    /// <summary>Made-up planting time: bigger trees and shrubs take longer; perennials are quick.</summary>
    private static decimal PlantingHours(TypeModel model, double height) => TypeModels.Family(model) == "perennial"
        ? 0.1m
        : decimal.Round(0.2m + (0.6m * (decimal)height), 2);

    /// <summary>Made-up hours per piece, meter or square meter for everything that is not a plant.</summary>
    private static decimal ThingHours(TypeModel model) => model switch
    {
        TypeModel.Pot => 0.1m,
        TypeModel.Rock => 0.3m,
        TypeModel.Furniture => 0.3m,
        TypeModel.Fence => 0.8m,
        TypeModel.SurfaceGrass => 0.15m,
        TypeModel.SurfacePaving => 0.6m,
        TypeModel.SurfaceGravel => 0.25m,
        TypeModel.SurfaceDecking => 1.0m,
        TypeModel.SurfaceBed => 0.3m,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Plants get their hours from PlantingHours."),
    };

    private static ArticleData Plant(
        string number, string name, string variety, string category, TypeModel model, decimal price, int zone, double height,
        double matureHeight, double matureWidth, double growth, Light light, string color, string? accent = null) =>
        new(number, name, variety, category, ArticleUnit.Piece, Money.InSek(price), StockStatus.InStock, model, 1, color, accent,
            zone, height, null, matureHeight, matureWidth, null, growth, light, PlantingHours(model, height));

    private static ArticleData Hedge(
        string number, string name, string variety, decimal price, int zone, double height, double width, double clipHeight,
        double growth, string color) =>
        new(number, name, variety, "Häckar och staket", ArticleUnit.Meter, Money.InSek(price), StockStatus.InStock, TypeModel.Hedge, 1,
            color, null, zone, height, width, null, null, clipHeight, growth, SunToPartShade, LaborHours: 0.5m);

    private static ArticleData Thing(
        string number, string name, string category, ArticleUnit unit, TypeModel model, decimal price, string color,
        string? accent = null, double scale = 1, double? height = null, double? width = null) =>
        new(number, name, null, category, unit, Money.InSek(price), StockStatus.InStock, model, scale, color, accent,
            null, height, width, null, null, null, null, Light.None, ThingHours(model));
}
