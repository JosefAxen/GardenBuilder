var builder = DistributedApplication.CreateBuilder(args);

// Resource names must match GardenBuilder.Contracts.ResourceNames.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();
var database = postgres.AddDatabase("gardenbuilder");

var storage = builder.AddAzureStorage("storage").RunAsEmulator(emulator => emulator.WithDataVolume());
var blobs = storage.AddBlobs("blobs");

// Migrations run as their own step; the API and the worker start only after it has completed.
var migrator = builder.AddProject<Projects.GardenBuilder_Migrator>("migrator")
    .WithReference(database).WaitFor(database);

// Fills the catalog with the example articles. Starts only when someone presses Start in the dashboard.
var exampleCatalog = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "assets", "catalog", "artiklar-exempel.xlsx"));
builder.AddProject<Projects.GardenBuilder_CatalogImport>("catalog-import")
    .WithReference(database).WaitForCompletion(migrator)
    .WithArgs("import", exampleCatalog)
    .WithExplicitStart();

var api = builder.AddProject<Projects.GardenBuilder_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.GardenBuilder_Worker>("worker")
    .WithReference(database).WaitFor(database)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health");

// Vite dev server. It proxies /api to the API (see src/web/vite.config.ts).
builder.AddViteApp("web", "../web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
