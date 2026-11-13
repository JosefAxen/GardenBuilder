using System.Net;

namespace GardenBuilder.IntegrationTests;

public sealed class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_is_ok_when_database_is_reachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_not_the_web_client()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
