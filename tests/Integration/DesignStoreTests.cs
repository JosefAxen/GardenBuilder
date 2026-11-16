using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Features.WorkingCopy;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

public sealed class DesignStoreTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A plot of size × size meters, so each scene is easy to tell apart.</summary>
    private static SceneDocument Scene(double size) => new(SceneDocument.CurrentFormatVersion,
    [
        new PlotElement(Guid.NewGuid(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
            [new Point2(0, 0), new Point2(size, 0), new Point2(size, size), new Point2(0, size)], Sloped: false),
    ]);

    private static string Json(SceneDocument scene) => SceneJson.Serialize(scene);

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<Modules.Design.Domain.Design> CreateAsync(Guid customerId, SceneDocument scene) =>
        InScope(s => s.GetRequiredService<WorkingCopyStore>().CreateAsync(Guid.NewGuid(), customerId, scene, Ct));

    private Task<SaveResult> SaveAsync(Guid id, Guid customerId, int revision, SceneDocument scene) =>
        InScope(s => s.GetRequiredService<WorkingCopyStore>().SaveAsync(id, customerId, revision, scene, Ct));

    private Task<Modules.Design.Domain.Design?> FindAsync(Guid id, Guid customerId) =>
        InScope(s => s.GetRequiredService<WorkingCopyStore>().FindAsync(id, customerId, Ct));

    [Fact]
    public async Task Save_with_current_revision_bumps_it()
    {
        var customer = Guid.NewGuid();
        var design = await CreateAsync(customer, Scene(10));
        Assert.Equal(1, design.Revision);

        var second = Scene(20);
        var result = await SaveAsync(design.Id, customer, 1, second);

        Assert.Equal(new SaveResult.Saved(2), result);
        var stored = await FindAsync(design.Id, customer);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Revision);
        Assert.Equal(Json(second), Json(SceneJson.Deserialize(stored.WorkingCopy)));
    }

    [Fact]
    public async Task Save_with_old_revision_is_conflict()
    {
        var customer = Guid.NewGuid();
        var design = await CreateAsync(customer, Scene(10));
        var second = Scene(20);
        await SaveAsync(design.Id, customer, 1, second);

        var result = await SaveAsync(design.Id, customer, 1, Scene(30));

        Assert.Equal(new SaveResult.Conflict(2), result);
        var stored = await FindAsync(design.Id, customer);
        Assert.Equal(Json(second), Json(SceneJson.Deserialize(stored!.WorkingCopy)));
    }

    [Fact]
    public async Task Save_by_other_customer_is_not_found()
    {
        var owner = Guid.NewGuid();
        var first = Scene(10);
        var design = await CreateAsync(owner, first);

        var result = await SaveAsync(design.Id, Guid.NewGuid(), 1, Scene(20));

        Assert.Equal(new SaveResult.NotFound(), result);
        var stored = await FindAsync(design.Id, owner);
        Assert.Equal(1, stored!.Revision);
        Assert.Equal(Json(first), Json(SceneJson.Deserialize(stored.WorkingCopy)));
        Assert.Null(await FindAsync(design.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Latest_is_the_most_recently_saved()
    {
        var customer = Guid.NewGuid();
        var first = await CreateAsync(customer, Scene(10));
        await CreateAsync(customer, Scene(20));

        await SaveAsync(first.Id, customer, 1, Scene(30));

        var latest = await InScope(s => s.GetRequiredService<WorkingCopyStore>().LatestAsync(customer, Ct));
        Assert.Equal(first.Id, latest?.Id);
        Assert.Null(await InScope(s => s.GetRequiredService<WorkingCopyStore>().LatestAsync(Guid.NewGuid(), Ct)));
    }

    [Fact]
    public async Task Version_freezes_the_working_copy()
    {
        var customer = Guid.NewGuid();
        var frozen = Scene(10);
        var design = await CreateAsync(customer, frozen);

        var first = await InScope(s => s.GetRequiredService<IDesignVersions>()
            .CreateAsync(design.Id, DesignVersionReason.QuoteRequest, Ct));
        var second = await InScope(s => s.GetRequiredService<IDesignVersions>()
            .CreateAsync(design.Id, DesignVersionReason.AiProposal, Ct));
        await SaveAsync(design.Id, customer, 1, Scene(20));

        Assert.Equal(1, first?.Number);
        Assert.Equal(2, second?.Number);
        var scene = await InScope(s => s.GetRequiredService<IDesignVersions>().GetAsync(first!.Id, Ct));
        Assert.Equal(Json(frozen), Json(scene!));
    }

    [Fact]
    public async Task Version_of_missing_design_is_null()
    {
        var version = await InScope(s => s.GetRequiredService<IDesignVersions>()
            .CreateAsync(Guid.NewGuid(), DesignVersionReason.StaffRevision, Ct));

        Assert.Null(version);
        Assert.Null(await InScope(s => s.GetRequiredService<IDesignVersions>().GetAsync(Guid.NewGuid(), Ct)));
    }
}
