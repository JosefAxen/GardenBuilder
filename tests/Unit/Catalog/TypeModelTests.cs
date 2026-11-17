using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Catalog.Domain;

namespace GardenBuilder.UnitTests.Catalog;

public sealed class TypeModelTests
{
    [Theory]
    [InlineData("tree-large", "TreeLarge")]
    [InlineData("conifer-column", "ConiferColumn")]
    [InlineData("surface-bed", "SurfaceBed")]
    [InlineData("hedge", "Hedge")]
    public void Key_round_trips(string key, string modelName)
    {
        var model = Enum.Parse<TypeModel>(modelName);

        Assert.True(TypeModels.TryParse(key, out var parsed));
        Assert.Equal(model, parsed);
        Assert.Equal(key, TypeModels.Key(model));
    }

    [Fact]
    public void Unknown_key_does_not_parse()
    {
        Assert.False(TypeModels.TryParse("palm", out _));
    }

    [Fact]
    public void Hedge_is_a_plant_sold_per_meter_as_a_line()
    {
        Assert.Equal(ArticleGeometry.Line, TypeModels.Geometry(TypeModel.Hedge));
        Assert.Equal(ArticleUnit.Meter, TypeModels.Unit(TypeModel.Hedge));
        Assert.True(TypeModels.IsPlant(TypeModel.Hedge));
    }

    [Fact]
    public void Surface_bed_has_material_bed()
    {
        Assert.Equal(SurfaceMaterial.Bed, TypeModels.Material(TypeModel.SurfaceBed));
        Assert.Null(TypeModels.Material(TypeModel.Pot));
    }

    [Fact]
    public void Every_type_model_has_a_key()
    {
        foreach (var model in Enum.GetValues<TypeModel>())
        {
            Assert.True(TypeModels.TryParse(TypeModels.Key(model), out var parsed));
            Assert.Equal(model, parsed);
        }
    }

    [Theory]
    [InlineData("tree-large", "deciduous-tree")]
    [InlineData("tree-fruit", "deciduous-tree")]
    [InlineData("tree-small", "deciduous-tree")]
    [InlineData("pine", "conifer")]
    [InlineData("spruce", "conifer")]
    [InlineData("conifer-column", "conifer")]
    [InlineData("shrub-round", "shrub")]
    [InlineData("shrub-flowering", "shrub")]
    [InlineData("perennial-tall", "perennial")]
    [InlineData("perennial-low", "perennial")]
    [InlineData("foliage-plant", "perennial")]
    [InlineData("ornamental-grass", "perennial")]
    [InlineData("rock", "furniture")]
    [InlineData("furniture", "furniture")]
    [InlineData("pot", "pot")]
    [InlineData("hedge", "hedge")]
    [InlineData("fence", "fence")]
    [InlineData("surface-grass", "surface-grass")]
    public void Every_form_has_its_family(string key, string family)
    {
        Assert.True(TypeModels.TryParse(key, out var model));
        Assert.Equal(family, TypeModels.Family(model));
    }

    [Theory]
    [InlineData("deciduous-tree")]
    [InlineData("conifer")]
    [InlineData("shrub")]
    [InlineData("perennial")]
    public void Old_keys_no_longer_parse(string key)
    {
        Assert.False(TypeModels.TryParse(key, out _));
    }

    [Theory]
    [InlineData("Rock", false)]
    [InlineData("Pot", false)]
    [InlineData("ConiferColumn", true)]
    [InlineData("FoliagePlant", true)]
    [InlineData("OrnamentalGrass", true)]
    public void IsPlant_follows_the_family(string model, bool isPlant)
    {
        Assert.Equal(isPlant, TypeModels.IsPlant(Enum.Parse<TypeModel>(model)));
    }
}
