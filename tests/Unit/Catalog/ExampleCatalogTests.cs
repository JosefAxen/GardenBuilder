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
        Assert.True(ExampleCatalog.Articles.Count(a => a.Variety == "Tuja Smaragd" && a.TypeModel == TypeModel.Conifer) >= 3);
        Assert.True(ExampleCatalog.Articles.Count(a => a.Variety == "Tuja Brabant" && a.TypeModel == TypeModel.Conifer) >= 2);
        Assert.True(ExampleCatalog.Articles.Count(a => a.TypeModel == TypeModel.Hedge && a.Variety?.Contains("Tuja", StringComparison.Ordinal) == true) >= 2);
    }

    [Fact]
    public void Example_covers_every_type_model()
    {
        Assert.All(Enum.GetValues<TypeModel>(), model => Assert.Contains(ExampleCatalog.Articles, a => a.TypeModel == model));
    }
}
