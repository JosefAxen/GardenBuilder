using GardenBuilder.Contracts;
using GardenBuilder.Contracts.Database;
using GardenBuilder.Modules.Design.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Design;

/// <summary>Scener, arbetskopior och designversioner.</summary>
public static class DesignModule
{
    public const string Schema = "design";

    public static IHostApplicationBuilder AddDesignModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<DesignDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema))
            .UseSnakeCaseNamingConvention());
        builder.EnrichNpgsqlDbContext<DesignDbContext>();
        builder.Services.AddScoped<IModuleMigrator, DesignMigrator>();

        return builder;
    }

    public static IEndpointRouteBuilder MapDesignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/designs").WithTags("Design");
        _ = group; // Map features here, e.g. group.MapSomething();

        return app;
    }
}
