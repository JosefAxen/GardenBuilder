using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Catalog;

public sealed class ArticleDataTests
{
    /// <summary>Tuja Smaragd 80–100 cm: a plant that breaks no rule.</summary>
    internal static ArticleData Valid() => new(
        ArticleNumber: "TU-SM-080",
        Name: "Tuja Smaragd 80–100 cm",
        Variety: "Tuja Smaragd",
        Category: "Träd",
        Unit: ArticleUnit.Piece,
        Price: Money.InSek(495m),
        StockStatus: StockStatus.InStock,
        TypeModel: TypeModel.Conifer,
        Scale: 1,
        Color: "#3f7a3a",
        AccentColor: null,
        HardinessZone: 5,
        Height: 0.9,
        Width: null,
        MatureHeight: 4,
        MatureWidth: 1.2,
        ClipHeight: null,
        GrowthCmPerYear: 20,
        Light: Light.Sun | Light.PartShade);

    private static readonly ArticleData Hedge = Valid() with
    {
        ArticleNumber = "TU-HA-060",
        Name = "Thujahäck Smaragd 60–80 cm",
        Unit = ArticleUnit.Meter,
        TypeModel = TypeModel.Hedge,
        Height = 0.7,
        Width = 0.5,
        MatureWidth = null,
        ClipHeight = 1.5,
    };

    [Fact]
    public void Valid_plant_has_no_errors()
    {
        Assert.Empty(Valid().Validate());
    }

    [Fact]
    public void Valid_hedge_has_no_errors()
    {
        Assert.Empty(Hedge.Validate());
    }

    [Fact]
    public void Plant_without_zone_is_an_error()
    {
        Assert.Contains("Växtzon krävs för växter.", (Valid() with { HardinessZone = null }).Validate());
    }

    [Fact]
    public void Plant_without_light_is_an_error()
    {
        Assert.Contains("Ljus krävs för växter.", (Valid() with { Light = Light.None }).Validate());
    }

    [Fact]
    public void Point_plant_without_mature_width_is_an_error()
    {
        Assert.Contains("Fullvuxen bredd krävs för träd, buskar och perenner.", (Valid() with { MatureWidth = null }).Validate());
    }

    [Fact]
    public void Plant_without_planting_height_is_an_error()
    {
        Assert.Contains("Höjd krävs för växter, häckar och staket.", (Valid() with { Height = null }).Validate());
    }

    [Fact]
    public void Unit_must_match_type_model()
    {
        Assert.Contains(
            "Enheten m passar inte typmodellen conifer, som säljs per st.",
            (Valid() with { Unit = ArticleUnit.Meter }).Validate());
    }

    [Fact]
    public void Hedge_needs_height_and_width()
    {
        Assert.Contains("Bredd krävs för häckar och staket.", (Hedge with { Width = null }).Validate());
    }

    [Fact]
    public void Price_must_be_positive()
    {
        Assert.Contains("Priset måste vara större än 0.", (Valid() with { Price = Money.InSek(0m) }).Validate());
    }

    [Fact]
    public void Price_has_at_most_two_decimals()
    {
        Assert.Contains("Priset får ha högst två decimaler.", (Valid() with { Price = Money.InSek(495.005m) }).Validate());
    }

    [Fact]
    public void Color_must_be_hex()
    {
        Assert.Contains("Färgen måste skrivas som #rrggbb.", (Valid() with { Color = "grön" }).Validate());
    }

    [Fact]
    public void Zone_must_be_between_1_and_8()
    {
        Assert.Contains("Växtzonen måste vara mellan 1 och 8.", (Valid() with { HardinessZone = 9 }).Validate());
    }

    [Fact]
    public void Name_is_required()
    {
        Assert.Contains("Namn krävs.", (Valid() with { Name = " " }).Validate());
    }

    [Fact]
    public void Name_longer_than_the_database_allows_is_an_error()
    {
        Assert.Contains("Namnet får vara högst 200 tecken.", (Valid() with { Name = new string('a', 201) }).Validate());
    }

    [Fact]
    public void Article_number_longer_than_the_database_allows_is_an_error()
    {
        Assert.Contains("Artikelnumret får vara högst 64 tecken.", (Valid() with { ArticleNumber = new string('1', 65) }).Validate());
    }

    [Fact]
    public void Zone_must_be_empty_for_things_that_are_not_plants()
    {
        var pot = Pot() with { HardinessZone = 5 };

        Assert.Contains(new ArticleRuleError(nameof(ArticleData.HardinessZone), "Växtzon ska vara tom för typmodellen pot."), pot.RuleErrors());
    }

    [Fact]
    public void Width_must_be_empty_for_a_tree()
    {
        Assert.Contains("Bredd ska vara tom för typmodellen conifer.", (Valid() with { Width = 1 }).Validate());
    }

    [Fact]
    public void Clip_height_is_only_for_hedges()
    {
        Assert.Contains("Klipphöjd ska vara tom för typmodellen conifer.", (Valid() with { ClipHeight = 1 }).Validate());
    }

    [Fact]
    public void Light_must_be_empty_for_things_that_are_not_plants()
    {
        Assert.Contains("Ljus ska vara tomt för typmodellen pot.", (Pot() with { Light = Light.Sun }).Validate());
    }

    [Fact]
    public void Missing_zone_names_its_field()
    {
        Assert.Contains(new ArticleRuleError(nameof(ArticleData.HardinessZone), "Växtzon krävs för växter."), (Valid() with { HardinessZone = null }).RuleErrors());
    }

    private static ArticleData Pot() => Valid() with
    {
        Name = "Terrakottakruka 40 cm",
        Variety = null,
        Category = "Krukor och möbler",
        TypeModel = TypeModel.Pot,
        HardinessZone = null,
        Height = null,
        MatureHeight = null,
        MatureWidth = null,
        GrowthCmPerYear = null,
        Light = Light.None,
        Scale = 1.2,
    };

    [Fact]
    public void Pot_needs_no_plant_fields()
    {
        var pot = Valid() with
        {
            Name = "Terrakottakruka 40 cm",
            Variety = null,
            Category = "Krukor och möbler",
            TypeModel = TypeModel.Pot,
            HardinessZone = null,
            Height = null,
            MatureHeight = null,
            MatureWidth = null,
            GrowthCmPerYear = null,
            Light = Light.None,
            Scale = 1.2,
        };

        Assert.Empty(pot.Validate());
    }
}
