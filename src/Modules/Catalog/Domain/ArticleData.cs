using System.Text.RegularExpressions;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>One broken rule. <see cref="Field"/> is the ArticleData property it is about.</summary>
internal sealed record ArticleRuleError(string Field, string Message);

/// <summary>
/// Everything that describes an article, as it comes from the import file. The rules for a valid article live in
/// <see cref="Validate"/>; the messages are Swedish because they are shown to whoever fixes the Excel file.
/// </summary>
/// <param name="Variety">Articles with the same variety are one plant in different sizes, e.g. "Tuja Smaragd".</param>
/// <param name="Height">Plants and hedges: planting height (as delivered) in meters. Fences: fence height.</param>
/// <param name="Width">Hedges and fences: the line's width in meters.</param>
/// <param name="ClipHeight">Hedges: the height the hedge is kept at. Information only.</param>
/// <param name="LaborHours">Hours of work to plant or lay one unit (piece, meter or square meter). Pricing uses it.</param>
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
    Light Light,
    decimal LaborHours)
{
    public const double MaxScale = 5;

    public const decimal MaxLaborHours = 100;

    // The database column sizes (ArticleConfiguration). Checked here so a long text gets a Swedish message with its
    // row instead of a database error.
    public const int MaxArticleNumberLength = 64;
    public const int MaxNameLength = 200;
    public const int MaxVarietyLength = 200;
    public const int MaxCategoryLength = 100;

    public static string UnitText(ArticleUnit unit) => unit switch
    {
        ArticleUnit.Piece => "st",
        ArticleUnit.Meter => "m",
        _ => "m²",
    };

    /// <summary>The broken rules, in Swedish. Empty when the article is valid.</summary>
    public IReadOnlyList<string> Validate() => [.. RuleErrors().Select(e => e.Message)];

    /// <summary>
    /// The broken rules, each with the field it is about (a property name of this record), so the import can point at
    /// the right Excel column. Empty when the article is valid.
    /// </summary>
    public IReadOnlyList<ArticleRuleError> RuleErrors()
    {
        var errors = new List<ArticleRuleError>();
        void Add(string field, string message) => errors.Add(new ArticleRuleError(field, message));

        var geometry = TypeModels.Geometry(TypeModel);
        var isPlant = TypeModels.IsPlant(TypeModel);
        var key = TypeModels.Key(TypeModel);

        if (string.IsNullOrWhiteSpace(ArticleNumber))
        {
            Add(nameof(ArticleNumber), "Artikelnummer krävs.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            Add(nameof(Name), "Namn krävs.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            Add(nameof(Category), "Kategori krävs.");
        }

        foreach (var (field, text, max, label) in new[]
        {
            (nameof(ArticleNumber), ArticleNumber, MaxArticleNumberLength, "Artikelnumret"),
            (nameof(Name), Name, MaxNameLength, "Namnet"),
            (nameof(Variety), Variety, MaxVarietyLength, "Sorten"),
            (nameof(Category), Category, MaxCategoryLength, "Kategorin"),
        })
        {
            if (text?.Length > max)
            {
                Add(field, $"{label} får vara högst {max} tecken.");
            }
        }

        var expectedUnit = TypeModels.Unit(TypeModel);
        if (Unit != expectedUnit)
        {
            Add(nameof(Unit), $"Enheten {UnitText(Unit)} passar inte typmodellen {key}, som säljs per {UnitText(expectedUnit)}.");
        }

        if (Price.Amount <= 0)
        {
            Add(nameof(Price), "Priset måste vara större än 0.");
        }

        if (decimal.Round(Price.Amount, 2) != Price.Amount)
        {
            Add(nameof(Price), "Priset får ha högst två decimaler.");
        }

        if (LaborHours is < 0 or > MaxLaborHours)
        {
            Add(nameof(LaborHours), $"Arbetstiden måste vara mellan 0 och {MaxLaborHours} timmar.");
        }

        if (decimal.Round(LaborHours, 2) != LaborHours)
        {
            Add(nameof(LaborHours), "Arbetstiden får ha högst två decimaler.");
        }

        if (Scale is <= 0 or > MaxScale)
        {
            Add(nameof(Scale), $"Skalan måste vara större än 0 och högst {MaxScale}.");
        }

        if (!HexColor().IsMatch(Color))
        {
            Add(nameof(Color), "Färgen måste skrivas som #rrggbb.");
        }

        if (AccentColor is not null && !HexColor().IsMatch(AccentColor))
        {
            Add(nameof(AccentColor), "Accentfärgen måste skrivas som #rrggbb.");
        }

        if (HardinessZone is < 1 or > 8)
        {
            Add(nameof(HardinessZone), "Växtzonen måste vara mellan 1 och 8.");
        }

        foreach (var (field, value, label) in new[]
        {
            (nameof(Height), Height, "Höjd"), (nameof(Width), Width, "Bredd"),
            (nameof(MatureHeight), MatureHeight, "Fullvuxen höjd"), (nameof(MatureWidth), MatureWidth, "Fullvuxen bredd"),
            (nameof(ClipHeight), ClipHeight, "Klipphöjd"), (nameof(GrowthCmPerYear), GrowthCmPerYear, "Tillväxt"),
        })
        {
            if (value is { } number && (!double.IsFinite(number) || number <= 0))
            {
                Add(field, $"{label} måste vara större än 0.");
            }
        }

        // What each type model needs.
        if (isPlant)
        {
            if (HardinessZone is null)
            {
                Add(nameof(HardinessZone), "Växtzon krävs för växter.");
            }

            if (Light == Light.None)
            {
                Add(nameof(Light), "Ljus krävs för växter.");
            }

            if (geometry == ArticleGeometry.Point && MatureWidth is null)
            {
                Add(nameof(MatureWidth), "Fullvuxen bredd krävs för träd, buskar och perenner.");
            }
        }

        var usesHeight = isPlant || TypeModel == TypeModel.Fence;
        if (usesHeight && Height is null)
        {
            Add(nameof(Height), "Höjd krävs för växter, häckar och staket.");
        }

        if (geometry == ArticleGeometry.Line && Width is null)
        {
            Add(nameof(Width), "Bredd krävs för häckar och staket.");
        }

        // What each type model must leave empty, so the file says only what is true for the article.
        foreach (var (field, isSet, allowed, label) in new[]
        {
            (nameof(HardinessZone), HardinessZone is not null, isPlant, "Växtzon"),
            (nameof(Height), Height is not null, usesHeight, "Höjd"),
            (nameof(Width), Width is not null, geometry == ArticleGeometry.Line, "Bredd"),
            (nameof(MatureHeight), MatureHeight is not null, isPlant, "Fullvuxen höjd"),
            (nameof(MatureWidth), MatureWidth is not null, isPlant, "Fullvuxen bredd"),
            (nameof(ClipHeight), ClipHeight is not null, TypeModel == TypeModel.Hedge, "Klipphöjd"),
            (nameof(GrowthCmPerYear), GrowthCmPerYear is not null, isPlant, "Tillväxt"),
        })
        {
            if (isSet && !allowed)
            {
                Add(field, $"{label} ska vara tom för typmodellen {key}.");
            }
        }

        if (!isPlant && Light != Light.None)
        {
            Add(nameof(Light), $"Ljus ska vara tomt för typmodellen {key}.");
        }

        return errors;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
