using GardenBuilder.Contracts;
using GardenBuilder.Modules.Catalog.Features.ListArticles;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Catalog;

/// <summary>Företagets sortiment och 3D-typmodeller.</summary>
public static class CatalogModule
{
    public const string Schema = "catalog";

    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<CatalogDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema)));
        builder.EnrichNpgsqlDbContext<CatalogDbContext>();

        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");
        group.MapListArticles();

        return app;
    }
}
