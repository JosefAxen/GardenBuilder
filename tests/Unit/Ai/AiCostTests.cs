using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class AiCostTests
{
    private static readonly AiPricingOptions Pricing = new()
    {
        UsdToSek = 10.50m,
        Prices = new() { ["claude-opus-5-5"] = new ModelPrice { InputPerMillionUsd = 4m, OutputPerMillionUsd = 20m } },
    };

    [Fact]
    public void Opus_5_5_costs_4_and_20_dollars_per_million_at_10_50()
    {
        // 1 M in = 4 USD, 0.1 M out = 2 USD; 6 USD at 10.50 = 63 kr.
        Assert.Equal(Money.InSek(63.00m), AiCost.Of("claude-opus-5-5", 1_000_000, 100_000, Pricing));
    }

    [Fact]
    public void A_model_without_a_price_has_no_cost()
    {
        Assert.Null(AiCost.Of("claude-x", 1_000, 1_000, Pricing));
    }

    [Fact]
    public void Cost_is_rounded_to_whole_ore()
    {
        Assert.Equal(Money.InSek(0.00m), AiCost.Of("claude-opus-5-5", 1, 1, Pricing));
        // 1 000 in and 500 out: 0.004 + 0.01 USD = 0.014 USD = 0.147 kr.
        Assert.Equal(Money.InSek(0.15m), AiCost.Of("claude-opus-5-5", 1_000, 500, Pricing));
    }

    [Fact]
    public void An_image_costs_its_fixed_price_per_image()
    {
        var pricing = new AiPricingOptions
        {
            UsdToSek = 10.50m,
            Prices = new()
            {
                ["test-image"] = new ModelPrice { PerImageUsd = 0m },
                ["image-x"] = new ModelPrice { PerImageUsd = 0.04m },
            },
        };

        Assert.Equal(Money.InSek(0m), AiCost.OfImage("test-image", pricing));
        Assert.Equal(Money.InSek(0.42m), AiCost.OfImage("image-x", pricing));
        Assert.Null(AiCost.OfImage("image-y", pricing));
    }
}

public sealed class AiSettingsTests
{
    [Fact]
    public void Defaults_are_500_kr_quotas_3_5_5_and_every_step_on()
    {
        var settings = AiSettings.Defaults;

        Assert.Equal(Money.InSek(500m), settings.DailyBudget);
        Assert.Equal(3, settings.Quotas[AiJobKinds.PhotoInterpretation]);
        Assert.Equal(5, settings.Quotas[AiJobKinds.DesignProposal]);
        Assert.Equal(5, settings.Quotas[AiJobKinds.DreamImage]);
        Assert.All(AiJobKinds.Steps, step => Assert.True(settings.Enabled[step]));
        Assert.Empty(settings.Validate());
    }

    [Theory]
    [InlineData(-1, 3, "Dagsbudgeten ska vara mellan 0 och 100 000 kr.")]
    [InlineData(100_001, 3, "Dagsbudgeten ska vara mellan 0 och 100 000 kr.")]
    [InlineData(500, 101, "Kvoten för fototolkning ska vara mellan 0 och 100.")]
    [InlineData(500, -1, "Kvoten för fototolkning ska vara mellan 0 och 100.")]
    public void Out_of_range_values_are_named(decimal budget, int photoQuota, string problem)
    {
        var settings = AiSettings.Defaults with
        {
            DailyBudget = Money.InSek(budget),
            Quotas = new Dictionary<string, int>(AiSettings.Defaults.Quotas) { [AiJobKinds.PhotoInterpretation] = photoQuota },
        };

        Assert.Contains(problem, settings.Validate());
    }

    [Fact]
    public void A_missing_step_is_a_problem()
    {
        var settings = AiSettings.Defaults with { Enabled = new Dictionary<string, bool>() };

        Assert.Contains("Alla tre AI-stegen måste anges.", settings.Validate());
    }
}
