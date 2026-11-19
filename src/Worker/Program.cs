using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Catalog;
using GardenBuilder.Modules.Design;
using GardenBuilder.Modules.Garden;
using GardenBuilder.Modules.Pricing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The worker is the only process that calls AI providers.
builder.AddAiModule().AddAiJobProcessing();

// Photo interpretation reads the garden's photos and the design's plot and house through their modules' contracts.
builder.AddGardenModule().AddDesignModule();

// Design proposals read the catalog's articles in stock and price each proposal with the editor's rules.
builder.AddCatalogModule().AddPricingModule();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
