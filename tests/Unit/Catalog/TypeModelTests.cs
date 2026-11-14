using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Catalog.Domain;

namespace GardenBuilder.UnitTests.Catalog;

public sealed class TypeModelTests
{
    [Theory]
    [InlineData("deciduous-tree", "DeciduousTree")]
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
}
