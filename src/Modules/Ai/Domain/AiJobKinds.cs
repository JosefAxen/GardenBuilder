namespace GardenBuilder.Modules.Ai.Domain;

/// <summary>The kinds of AI job. The three steps have quotas and switches; the connection test has neither.</summary>
internal static class AiJobKinds
{
    public const string ConnectionTest = "connection-test";
    public const string PhotoInterpretation = "photo-interpretation";
    public const string DesignProposal = "design-proposal";
    public const string DreamImage = "dream-image";

    /// <summary>The customer-facing AI steps, each with a quota per garden and an on/off switch.</summary>
    public static IReadOnlyList<string> Steps { get; } = [PhotoInterpretation, DesignProposal, DreamImage];

    public static bool IsStep(string kind) => Steps.Contains(kind);

    /// <summary>The step's name in Swedish, for messages.</summary>
    public static string SwedishName(string step) => step switch
    {
        PhotoInterpretation => "fototolkning",
        DesignProposal => "designförslag",
        DreamImage => "drömbild",
        _ => step,
    };
}
