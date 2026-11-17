using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Features.ImportArticles;

namespace GardenBuilder.UnitTests.Catalog;

public sealed class ExampleCatalogTests
{
    [Fact]
    public void Every_example_article_is_valid()
    {
        Assert.All(ExampleCatalog.Articles, a => Assert.Empty(a.Validate()));
        Assert.Equal(ExampleCatalog.Articles.Count, ExampleCatalog.Articles.Select(a => a.ArticleNumber).Distinct().Count());
    }

    [Fact]
    public void Example_has_thujas_in_several_sizes()
    {
        Assert.True(ExampleCatalog.Articles.Count(a => a.Variety == "Tuja Smaragd" && a.TypeModel == TypeModel.ConiferColumn) >= 3);
        Assert.True(ExampleCatalog.Articles.Count(a => a.Variety == "Tuja Brabant" && a.TypeModel == TypeModel.ConiferColumn) >= 2);
        Assert.True(ExampleCatalog.Articles.Count(a => a.TypeModel == TypeModel.Hedge && a.Variety?.Contains("Tuja", StringComparison.Ordinal) == true) >= 2);
    }

    [Theory]
    [InlineData("BA-SM-120", "ConiferColumn")]
    [InlineData("TR-AP-150", "TreeFruit")]
    [InlineData("TR-BJ-200", "TreeLarge")]
    [InlineData("TR-LO-060", "TreeSmall")]
    [InlineData("BA-GR-100", "Spruce")]
    [InlineData("BA-TA-030", "ShrubRound")]
    [InlineData("BU-BX-030", "ShrubRound")]
    [InlineData("BU-HO-040", "ShrubFlowering")]
    [InlineData("BU-SY-060", "ShrubFlowering")]
    [InlineData("BU-RH-040", "ShrubFlowering")]
    [InlineData("BU-SP-030", "ShrubFlowering")]
    [InlineData("PE-FU-001", "FoliagePlant")]
    [InlineData("PE-LA-001", "PerennialTall")]
    [InlineData("KR-TE-040", "Pot")]
    [InlineData("MO-BA-150", "Furniture")]
    public void Forms_match_the_design(string articleNumber, string form)
    {
        Assert.Equal(Enum.Parse<TypeModel>(form), ExampleCatalog.Articles.Single(a => a.ArticleNumber == articleNumber).TypeModel);
    }

    [Fact]
    public void Committed_example_file_matches_the_example_catalog()
    {
        var path = Path.Combine(RepoRoot.Path, "assets", "catalog", "artiklar-exempel.xlsx");
        const string fix = "Run: dotnet run --project tools/CatalogImport -- example assets/catalog/artiklar-exempel.xlsx";
        Assert.True(File.Exists(path), $"{path} is missing. {fix}");

        using var file = File.OpenRead(path);
        var sheet = ArticleSheet.Read(file);

        Assert.Empty(sheet.Errors);
        Assert.True(ExampleCatalog.Articles.SequenceEqual(sheet.Articles), $"The committed example file is out of date. {fix}");
    }

    [Fact]
    public void Every_example_article_has_labor_hours()
    {
        Assert.All(ExampleCatalog.Articles, a => Assert.True(a.LaborHours > 0, $"{a.ArticleNumber} has no labor hours."));
    }

    [Fact]
    public void Example_covers_every_type_model()
    {
        Assert.All(Enum.GetValues<TypeModel>(), model => Assert.Contains(ExampleCatalog.Articles, a => a.TypeModel == model));
    }
}
