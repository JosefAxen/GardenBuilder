using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>
/// An item in the company's assortment, in one size: "Tuja Smaragd 40–60 cm" and "Tuja Smaragd 80–100 cm" are two
/// articles. Only articles from the catalog can be used in a design. An article is never deleted, only discontinued,
/// because saved designs may point at it.
/// </summary>
internal sealed class Article
{
    private Article()
    {
        ArticleNumber = string.Empty;
        Name = string.Empty;
        Category = string.Empty;
        Color = string.Empty;
    }

    public Guid Id { get; private set; }

    public string ArticleNumber { get; private set; }

    public string Name { get; private set; }

    public string? Variety { get; private set; }

    public string Category { get; private set; }

    public ArticleUnit Unit { get; private set; }

    /// <summary>Price per unit in SEK including VAT.</summary>
    public Money Price { get; private set; }

    public StockStatus StockStatus { get; private set; }

    public TypeModel TypeModel { get; private set; }

    public double Scale { get; private set; }

    public string Color { get; private set; }

    public string? AccentColor { get; private set; }

    public int? HardinessZone { get; private set; }

    /// <summary>Plants and hedges: planting height in meters. Fences: fence height.</summary>
    public double? Height { get; private set; }

    /// <summary>Hedges and fences: the line's width in meters.</summary>
    public double? Width { get; private set; }

    public double? MatureHeight { get; private set; }

    public double? MatureWidth { get; private set; }

    public double? ClipHeight { get; private set; }

    public double? GrowthCmPerYear { get; private set; }

    public Light Light { get; private set; }

    /// <summary>Hours of work per unit, for the labor part of the price.</summary>
    public decimal LaborHours { get; private set; }

    public static Article Create(ArticleData data)
    {
        var article = new Article { Id = Ids.New() };
        article.Update(data);
        return article;
    }

    /// <summary>Replaces everything with <paramref name="data"/>. The id never changes.</summary>
    public void Update(ArticleData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = data.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid article {data.ArticleNumber}: {string.Join(" ", errors)}", nameof(data));
        }

        ArticleNumber = data.ArticleNumber;
        Name = data.Name;
        Variety = data.Variety;
        Category = data.Category;
        Unit = data.Unit;
        Price = data.Price;
        StockStatus = data.StockStatus;
        TypeModel = data.TypeModel;
        Scale = data.Scale;
        Color = data.Color;
        AccentColor = data.AccentColor;
        HardinessZone = data.HardinessZone;
        Height = data.Height;
        Width = data.Width;
        MatureHeight = data.MatureHeight;
        MatureWidth = data.MatureWidth;
        ClipHeight = data.ClipHeight;
        GrowthCmPerYear = data.GrowthCmPerYear;
        Light = data.Light;
        LaborHours = data.LaborHours;
    }

    public void Discontinue() => StockStatus = StockStatus.Discontinued;

    /// <summary>The article as import data, so an import can tell whether anything changed.</summary>
    public ArticleData ToData() => new(
        ArticleNumber, Name, Variety, Category, Unit, Price, StockStatus, TypeModel, Scale, Color, AccentColor,
        HardinessZone, Height, Width, MatureHeight, MatureWidth, ClipHeight, GrowthCmPerYear, Light, LaborHours);
}

internal enum ArticleUnit
{
    Piece,
    Meter,
    SquareMeter,
}

internal enum StockStatus
{
    InStock,
    OutOfStock,
    Discontinued,
}
