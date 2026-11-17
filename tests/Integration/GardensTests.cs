using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Modules.Garden.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

public sealed class GardensTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_saves_a_garden_for_the_customer()
    {
        var customerId = Guid.NewGuid();
        Guid gardenId;
        using (var scope = factory.Services.CreateScope())
        {
            gardenId = await scope.ServiceProvider.GetRequiredService<IGardens>()
                .CreateAsync(customerId, TestContext.Current.CancellationToken);
        }

        using var read = factory.Services.CreateScope();
        var gardens = await read.ServiceProvider.GetRequiredService<GardenDbContext>().Gardens
            .Where(g => g.Id == gardenId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var garden = Assert.Single(gardens);
        Assert.Equal(customerId, garden.CustomerId);
        Assert.InRange(garden.CreatedAt, DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
