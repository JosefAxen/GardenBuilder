using System.Reflection;
using NetArchTest.Rules;

namespace GardenBuilder.ArchitectureTests;

/// <summary>Breaks the build when a module reaches into another module. See docs/design.md, "Regler för modulgränser".</summary>
public sealed class ModuleBoundaryTests
{
    private const string ModulePrefix = "GardenBuilder.Modules.";

    private static readonly Dictionary<string, Assembly> Modules = new()
    {
        ["Garden"] = typeof(Modules.Garden.GardenModule).Assembly,
        ["Catalog"] = typeof(Modules.Catalog.CatalogModule).Assembly,
        ["Design"] = typeof(Modules.Design.DesignModule).Assembly,
        ["Pricing"] = typeof(Modules.Pricing.PricingModule).Assembly,
        ["Leads"] = typeof(Modules.Leads.LeadsModule).Assembly,
        ["Ai"] = typeof(Modules.Ai.AiModule).Assembly,
        ["Identity"] = typeof(Modules.Identity.IdentityModule).Assembly,
    };

    public static TheoryData<string> ModuleNames => [.. Modules.Keys];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_does_not_depend_on_other_modules(string module)
    {
        var others = Modules.Keys.Where(m => m != module).Select(m => ModulePrefix + m).ToArray();

        var result = Types.InAssembly(Modules[module]).ShouldNot().HaveDependencyOnAny(others).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void SharedKernel_is_pure()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Money).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "GardenBuilder.Modules", "GardenBuilder.Contracts")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Contracts_do_not_depend_on_modules_or_infrastructure()
    {
        var result = Types.InAssembly(typeof(Contracts.ResourceNames).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("GardenBuilder.Modules", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
