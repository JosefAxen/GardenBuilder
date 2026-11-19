using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Contracts.Pricing;

/// <summary>A scene's approximate price, including work and VAT.</summary>
public sealed record PriceSpan(Money Low, Money High)
{
    /// <summary>Halfway between low and high, used to compare with a budget.</summary>
    public Money Middle => Money.InSek((Low.Amount + High.Amount) / 2);
}

/// <summary>Prices a scene with the same rules as the editor's price, for modules such as design proposals.</summary>
public interface IPriceEstimates
{
    Task<PriceSpan> EstimateAsync(SceneDocument scene, CancellationToken cancellationToken);
}
