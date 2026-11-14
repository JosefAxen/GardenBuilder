using System.Text.RegularExpressions;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>
/// Everything that describes an article, as it comes from the import file. The rules for a valid article live in
/// <see cref="Validate"/>; the messages are Swedish because they are shown to whoever fixes the Excel file.
/// </summary>
/// <param name="Variety">Articles with the same variety are one plant in different sizes, e.g. "Tuja Smaragd".</param>
/// <param name="Height">Plants and hedges: planting height (as delivered) in meters. Fences: fence height.</param>
/// <param name="Width">Hedges and fences: the line's width in meters.</param>
/// <param name="ClipHeight">Hedges: the height the hedge is kept at. Information only.</param>
internal sealed partial record ArticleData(
    string ArticleNumber,
    string Name,
    string? Variety,
    string Category,
    ArticleUnit Unit,
    Money Price,
    StockStatus StockStatus,
    TypeModel TypeModel,
    double Scale,
    string Color,
    string? AccentColor,
    int? HardinessZone,
    double? Height,
    double? Width,
    double? MatureHeight,
    double? MatureWidth,
    double? ClipHeight,
    double? GrowthCmPerYear,
    Light Light)
{
    public const double MaxScale = 5;

    public static string UnitText(ArticleUnit unit) => unit switch
    {
        ArticleUnit.Piece => "st",
        ArticleUnit.Meter => "m",
        _ => "m²",
    };

    /// <summary>The broken rules, in Swedish. Empty when the article is valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var geometry = TypeModels.Geometry(TypeModel);
        var isPlant = TypeModels.IsPlant(TypeModel);

        if (string.IsNullOrWhiteSpace(ArticleNumber))
        {
            errors.Add("Artikelnummer krävs.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Namn krävs.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            errors.Add("Kategori krävs.");
        }

        var expectedUnit = TypeModels.Unit(TypeModel);
        if (Unit != expectedUnit)
        {
            errors.Add($"Enheten {UnitText(Unit)} passar inte typmodellen {TypeModels.Key(TypeModel)}, som säljs per {UnitText(expectedUnit)}.");
        }

        if (Price.Amount <= 0)
        {
            errors.Add("Priset måste vara större än 0.");
        }

        if (decimal.Round(Price.Amount, 2) != Price.Amount)
        {
            errors.Add("Priset får ha högst två decimaler.");
        }

        if (Scale is <= 0 or > MaxScale)
        {
            errors.Add($"Skalan måste vara större än 0 och högst {MaxScale}.");
        }

        if (!HexColor().IsMatch(Color))
        {
            errors.Add("Färgen måste skrivas som #rrggbb.");
        }

        if (AccentColor is not null && !HexColor().IsMatch(AccentColor))
        {
            errors.Add("Accentfärgen måste skrivas som #rrggbb.");
        }

        if (HardinessZone is < 1 or > 8)
        {
            errors.Add("Växtzonen måste vara mellan 1 och 8.");
        }

        foreach (var (value, label) in new[]
        {
            (Height, "Höjd"), (Width, "Bredd"), (MatureHeight, "Fullvuxen höjd"),
            (MatureWidth, "Fullvuxen bredd"), (ClipHeight, "Klipphöjd"), (GrowthCmPerYear, "Tillväxt"),
        })
        {
            if (value is { } number && (!double.IsFinite(number) || number <= 0))
            {
                errors.Add($"{label} måste vara större än 0.");
            }
        }

        if (isPlant)
        {
            if (HardinessZone is null)
            {
                errors.Add("Växtzon krävs för växter.");
            }

            if (Light == Light.None)
            {
                errors.Add("Ljus krävs för växter.");
            }

            if (geometry == ArticleGeometry.Point && MatureWidth is null)
            {
                errors.Add("Fullvuxen bredd krävs för träd, buskar och perenner.");
            }
        }

        if ((isPlant || TypeModel == TypeModel.Fence) && Height is null)
        {
            errors.Add("Höjd krävs för växter, häckar och staket.");
        }

        if (geometry == ArticleGeometry.Line && Width is null)
        {
            errors.Add("Bredd krävs för häckar och staket.");
        }

        return errors;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
