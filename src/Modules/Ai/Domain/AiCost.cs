using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Domain;

/// <summary>A model's price in US dollars: per million tokens for text, a fixed amount per image for images.</summary>
internal sealed class ModelPrice
{
    public decimal InputPerMillionUsd { get; set; }

    public decimal OutputPerMillionUsd { get; set; }

    public decimal PerImageUsd { get; set; }
}

/// <summary>Prices per model and the exchange rate, bound from the "Ai" section of the settings.</summary>
internal sealed class AiPricingOptions
{
    public const string Section = "Ai";

    public Dictionary<string, ModelPrice> Prices { get; set; } = new(StringComparer.Ordinal);

    public decimal UsdToSek { get; set; }

    public bool HasPrice(string model) => Prices.ContainsKey(model);
}

/// <summary>
/// What an AI call cost in kronor: tokens times the model's price, times the exchange rate. An estimate; the provider's
/// invoice is the truth.
/// </summary>
internal static class AiCost
{
    /// <summary>The cost rounded to whole öre, or null when the model has no price (it must then never be called).</summary>
    public static Money? Of(string model, int inputTokens, int outputTokens, AiPricingOptions pricing)
    {
        if (!pricing.Prices.TryGetValue(model, out var price))
        {
            return null;
        }

        var usd = (inputTokens * price.InputPerMillionUsd + outputTokens * price.OutputPerMillionUsd) / 1_000_000m;
        return Money.InSek(Math.Round(usd * pricing.UsdToSek, 2, MidpointRounding.ToEven));
    }

    /// <summary>What one image cost, rounded to whole öre, or null when the model has no price.</summary>
    public static Money? OfImage(string model, AiPricingOptions pricing) =>
        pricing.Prices.TryGetValue(model, out var price)
            ? Money.InSek(Math.Round(price.PerImageUsd * pricing.UsdToSek, 2, MidpointRounding.ToEven))
            : null;
}
