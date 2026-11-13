using GardenBuilder.Contracts;
using GardenBuilder.Modules.Garden.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema)));
        builder.EnrichNpgsqlDbContext<GardenDbContext>();

        return builder;
    }

    public static IEndpointRouteBuilder MapGardenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gardens").WithTags("Garden");
        _ = group; // Map features here, e.g. group.MapSomething();

        return app;
    }
}
