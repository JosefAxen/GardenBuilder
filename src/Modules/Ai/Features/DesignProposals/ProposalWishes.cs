namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>What the customer asks a design proposal for (docs/superpowers/specs/2026-11-18-design-proposals-design.md).</summary>
/// <param name="Style">"natural", "modern", "classic" or "rural".</param>
/// <param name="Budget">"under-50k", "50k-150k", "150k-300k" or "over-300k", including work and VAT.</param>
/// <param name="Care">"low", "medium" or "high".</param>
/// <param name="Zone">The customer's hardiness zone, 1–8.</param>
/// <param name="Note">Free text, at most 300 characters.</param>
internal sealed record ProposalWishes(string Style, string Budget, string Care, int Zone, string? Note)
{
    public const int MaxNote = 300;

    public static IReadOnlyDictionary<string, string> Styles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["natural"] = "Naturlig", ["modern"] = "Modern", ["classic"] = "Klassisk", ["rural"] = "Lantlig",
    };

    /// <summary>The budget's ceiling in SEK, or null when there is none.</summary>
    public static IReadOnlyDictionary<string, decimal?> Budgets { get; } = new Dictionary<string, decimal?>(StringComparer.Ordinal)
    {
        ["under-50k"] = 50_000m, ["50k-150k"] = 150_000m, ["150k-300k"] = 300_000m, ["over-300k"] = null,
    };

    public static IReadOnlyDictionary<string, string> CareLevels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["low"] = "Låg", ["medium"] = "Medel", ["high"] = "Hög",
    };

    public decimal? Ceiling => Budgets.GetValueOrDefault(Budget);

    /// <summary>What is wrong, in Swedish for the customer. Empty when the wishes can be used.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (!Styles.ContainsKey(Style ?? string.Empty))
        {
            problems.Add("Välj en stil i listan.");
        }

        if (!Budgets.ContainsKey(Budget ?? string.Empty))
        {
            problems.Add("Välj en budget i listan.");
        }

        if (!CareLevels.ContainsKey(Care ?? string.Empty))
        {
            problems.Add("Välj en skötselnivå i listan.");
        }

        if (Zone is < 1 or > 8)
        {
            problems.Add("Växtzonen ska vara mellan 1 och 8.");
        }

        if (Note is { Length: > MaxNote })
        {
            problems.Add($"Önskemålet får vara högst {MaxNote} tecken.");
        }

        return problems;
    }
}
