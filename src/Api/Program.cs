using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Catalog;
using GardenBuilder.Modules.Design;
using GardenBuilder.Modules.Garden;
using GardenBuilder.Modules.Identity;
using GardenBuilder.Modules.Leads;
using GardenBuilder.Modules.Pricing;

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
app.UseStaticFiles();

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
public partial class Program;
