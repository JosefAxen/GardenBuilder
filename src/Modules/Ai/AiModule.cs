using GardenBuilder.Contracts;
using GardenBuilder.Contracts.Database;
using GardenBuilder.Modules.Ai.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Ai;

/// <summary>AI-jobb, promptversioner, leverantörer och kostnader.</summary>
public static class AiModule
{
    public const string Schema = "ai";

    public static IHostApplicationBuilder AddAiModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<AiDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema))
            .UseSnakeCaseNamingConvention());
        builder.EnrichNpgsqlDbContext<AiDbContext>();
        builder.Services.AddScoped<IModuleMigrator, AiMigrator>();

        return builder;
    }

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").WithTags("Ai");
        _ = group; // Map features here, e.g. group.MapSomething();

        return app;
    }
}
