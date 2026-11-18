using GardenBuilder.Contracts;
using GardenBuilder.Contracts.Database;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Modules.Garden.Features.CreateGarden;
using GardenBuilder.Modules.Garden.Features.Photos;
using GardenBuilder.Modules.Garden.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Garden;

/// <summary>Kundens tomt, foton och nuläge.</summary>
public static class GardenModule
{
    public const string Schema = "garden";

    public static IHostApplicationBuilder AddGardenModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<GardenDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema))
            .UseSnakeCaseNamingConvention());
        builder.EnrichNpgsqlDbContext<GardenDbContext>();
        builder.Services.AddScoped<IModuleMigrator, GardenMigrator>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IGardens, Gardens>();
        builder.AddAzureBlobServiceClient(ResourceNames.Blobs);
        builder.Services.AddScoped<PhotoStore>();
        builder.Services.AddScoped<IGardenPhotos, GardenPhotos>();

        return builder;
    }

    public static IEndpointRouteBuilder MapGardenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/gardens").WithTags("Garden").MapPhotos();

        return app;
    }
}
