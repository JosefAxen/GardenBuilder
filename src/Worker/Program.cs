using GardenBuilder.Modules.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The worker is the only process that calls AI providers.
builder.AddAiModule().AddAiJobProcessing();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
