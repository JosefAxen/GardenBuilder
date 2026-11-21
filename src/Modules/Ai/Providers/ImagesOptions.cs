using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Providers;

/// <summary>The "Images" settings: which image provider runs and with which model. Keys live in user secrets only.</summary>
internal sealed class ImagesOptions
{
    public const string Section = "Images";

    /// <summary>"test" until real keys exist.</summary>
    public string Provider { get; set; } = TestImageModel.ProviderName;

    public string Model { get; set; } = TestImageModel.ModelName;

    /// <summary>A model per kind of job, e.g. "dream-image": "test-image". Kinds not listed use <see cref="Model"/>.</summary>
    public Dictionary<string, string> Models { get; set; } = [];

    /// <summary>How long the test model pretends to work.</summary>
    public double TestDelaySeconds { get; set; } = 3;

    /// <summary>When above 0, every n:th call to the test model fails on purpose, to try the error path.</summary>
    public int TestFailEvery { get; set; }

    public string ModelFor(string kind) => Models.TryGetValue(kind, out var model) && !string.IsNullOrWhiteSpace(model) ? model : Model;

    /// <summary>Every model a job may use, for checking at start that each has a price.</summary>
    public IEnumerable<string> AllModels => Models.Values.Append(Model).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.Ordinal);

    /// <summary>The image model the settings name. An unknown provider fails when the model is first needed.</summary>
    public static IImageModel Create(IOptions<ImagesOptions> options) => options.Value.Provider switch
    {
        TestImageModel.ProviderName => new TestImageModel(options),
        var other => throw new InvalidOperationException($"Unknown image provider '{other}'."),
    };
}
