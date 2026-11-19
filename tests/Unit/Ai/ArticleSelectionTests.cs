using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Ai.Features.DesignProposals;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class ArticleSelectionTests
{
    private static CatalogArticle Plant(string name, int zone, params string[] light) => new(
        Ids.New(), name, "piece", Money.InSek(300m), "shrub-round", "shrub", "point", null, zone, 0.5, null, 1.5, 1.2, light);

    private static CatalogArticle Slabs() => new(
        Ids.New(), "Betongplatta", "square-meter", Money.InSek(450m), "surface-paving", "surface-paving", "surface", "paving",
        null, null, null, null, null, []);

    private static Zone ZoneWith(ZoneLight light) => new($"z-{light}", "zon", ZoneKind.Middle, null, [], 20, light);

    private static readonly Zone[] SunAndShade = [ZoneWith(ZoneLight.Sun), ZoneWith(ZoneLight.Shade)];

    [Fact]
    public void Zone_v_plant_fits_zone_three_but_not_zone_six()
    {
        var plant = Plant("Syren", 5, "sun");

        Assert.Contains(plant, ArticleSelection.Select([plant], 3, SunAndShade));
        Assert.DoesNotContain(plant, ArticleSelection.Select([plant], 6, SunAndShade));
        Assert.Contains(plant, ArticleSelection.Select([plant], 5, SunAndShade));
    }

    [Fact]
    public void Surfaces_are_always_offered()
    {
        var slabs = Slabs();

        Assert.Contains(slabs, ArticleSelection.Select([slabs], 8, SunAndShade));
    }

    [Fact]
    public void Never_more_than_150()
    {
        var many = Enumerable.Range(0, 400).Select(i => Plant($"Växt {i:000}", 8, "sun")).Append(Slabs()).ToList();

        var selected = ArticleSelection.Select(many, 1, SunAndShade);

        Assert.Equal(ArticleSelection.Max, selected.Count);
        Assert.Equal(150, ArticleSelection.Max);
        Assert.Contains(selected, a => a.Geometry == "surface");
    }

    [Fact]
    public void Plants_that_suit_more_zones_come_first()
    {
        var both = Plant("Ölandstok", 5, "sun", "shade");
        var sunOnly = Plant("Lavendel", 5, "sun");
        var neither = Plant("Ormbunke", 5, "part-shade");

        var selected = ArticleSelection.Select([neither, sunOnly, both], 3, SunAndShade);

        Assert.Equal([both, sunOnly, neither], selected);
    }

    [Theory]
    [InlineData("gothic", "50k-150k", "low", 3, null, "Välj en stil i listan.")]
    [InlineData("modern", "1-million", "low", 3, null, "Välj en budget i listan.")]
    [InlineData("modern", "50k-150k", "none", 3, null, "Välj en skötselnivå i listan.")]
    [InlineData("modern", "50k-150k", "low", 9, null, "Växtzonen ska vara mellan 1 och 8.")]
    [InlineData("modern", "50k-150k", "low", 0, null, "Växtzonen ska vara mellan 1 och 8.")]
    public void Out_of_range_wishes_are_named_in_swedish(string style, string budget, string care, int zone, string? note, string problem)
    {
        Assert.Contains(problem, new ProposalWishes(style, budget, care, zone, note).Problems());
    }

    [Fact]
    public void A_note_over_300_characters_is_a_problem()
    {
        var wishes = new ProposalWishes("natural", "under-50k", "medium", 3, new string('a', 301));

        Assert.Equal(["Önskemålet får vara högst 300 tecken."], wishes.Problems());
        Assert.Empty((wishes with { Note = new string('a', 300) }).Problems());
    }

    [Theory]
    [InlineData("under-50k", 50_000)]
    [InlineData("50k-150k", 150_000)]
    [InlineData("150k-300k", 300_000)]
    [InlineData("over-300k", null)]
    public void Ceilings_are_50_150_300_and_none(string budget, int? ceiling)
    {
        Assert.Equal((decimal?)ceiling, new ProposalWishes("classic", budget, "high", 4, null).Ceiling);
    }
}
