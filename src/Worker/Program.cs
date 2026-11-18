using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Design;
using GardenBuilder.Modules.Garden;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The worker is the only process that calls AI providers.
builder.AddAiModule().AddAiJobProcessing();

// Photo interpretation reads the garden's photos and the design's plot and house through their modules' contracts.
builder.AddGardenModule().AddDesignModule();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
