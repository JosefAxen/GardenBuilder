using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Catalog;
using GardenBuilder.Modules.Design;
using GardenBuilder.Modules.Garden;
using GardenBuilder.Modules.Identity;
using GardenBuilder.Modules.Leads;
using GardenBuilder.Modules.Pricing;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder
    .AddIdentityModule()
    .AddGardenModule()
    .AddCatalogModule()
    .AddDesignModule()
    .AddPricingModule()
    .AddLeadsModule()
    .AddAiModule();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The API also serves the built web client (src/web builds into wwwroot), so everything has the same origin.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = Program.WebClientContentTypes() });

// Who is calling: the customer or staff cookie (Identity module). Before the endpoints, after the static files.
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapIdentityEndpoints()
    .MapGardenEndpoints()
    .MapCatalogEndpoints()
    .MapDesignEndpoints()
    .MapPricingEndpoints()
    .MapLeadsEndpoints()
    .MapAiEndpoints();

// Unknown API routes are 404, everything else falls back to the single-page app.
app.MapFallback("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Entry point, public so integration tests can use WebApplicationFactory.</summary>
public partial class Program
{
    /// <summary>
    /// The static file types the web client needs. ASP.NET Core does not know .glb, the editor's 3D models
    /// (docs/superpowers/specs/2026-11-17-model-library-design.md), and does not serve file types it does not know.
    /// </summary>
    public static FileExtensionContentTypeProvider WebClientContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".glb"] = "model/gltf-binary";
        return provider;
    }
}
