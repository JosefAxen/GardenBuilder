using GardenBuilder.Contracts.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Helpers for the identity tests: an https client, and a request context without HTTP for services.</summary>
internal static class IdentityTestHelpers
{
    /// <summary>A client on https, so Secure cookies are sent. Cookies are handled by hand when handleCookies is false.</summary>
    public static HttpClient HttpsClient(WebApplicationFactory<Program> factory, bool handleCookies = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = handleCookies });

    /// <summary>The value of one cookie in a Set-Cookie header, or null when the response did not set it.</summary>
    public static string? SetCookie(IHeaderDictionary headers, string name) =>
        headers.SetCookie.Select(c => c ?? string.Empty).FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal));
}

/// <summary>
/// One fake request, served by a fresh scope of the real API services. Lets a test call ICurrentCustomer the way an
/// endpoint would, and read the cookies it sets, without an endpoint that uses it (delprojekt 4 adds those).
/// </summary>
internal sealed class RequestContext : IDisposable
{
    private readonly IServiceScope _scope;

    public RequestContext(WebApplicationFactory<Program> factory, string? cookie = null)
    {
        _scope = factory.Services.CreateScope();
        Http = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
        Http.Request.Scheme = "https";
        Http.Request.Host = new HostString("localhost");
        if (cookie is not null)
        {
            Http.Request.Headers.Cookie = cookie;
        }

        _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = Http;
    }

    public DefaultHttpContext Http { get; }

    public ICurrentCustomer Customer => _scope.ServiceProvider.GetRequiredService<ICurrentCustomer>();

    public IServiceProvider Services => _scope.ServiceProvider;

    /// <summary>The "name=value" part of a cookie this request set, ready to send with the next one.</summary>
    public string? CookieSet(string name) => IdentityTestHelpers.SetCookie(Http.Response.Headers, name)?.Split(';')[0];

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
        _scope.Dispose();
    }
}
