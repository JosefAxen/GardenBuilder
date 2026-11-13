using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GardenBuilder.IntegrationTests;

public sealed class CatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_articles_returns_empty_list_on_fresh_database()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/catalog/articles", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var articles = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, articles.ValueKind);
        Assert.Equal(0, articles.GetArrayLength());
    }
}
