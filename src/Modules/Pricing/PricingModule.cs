using GardenBuilder.Contracts;
using GardenBuilder.Contracts.Database;
using GardenBuilder.Modules.Pricing.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Pricing;

/// <summary>Prislistor, arbetsnormer, prisregler och prisberäkning.</summary>
public static class PricingModule
{
    public const string Schema = "pricing";

    public static IHostApplicationBuilder AddPricingModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<PricingDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema))
            .UseSnakeCaseNamingConvention());
        builder.EnrichNpgsqlDbContext<PricingDbContext>();
        builder.Services.AddScoped<IModuleMigrator, PricingMigrator>();

        return builder;
    }

    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pricing").WithTags("Pricing");
        _ = group; // Map features here, e.g. group.MapSomething();

        return app;
    }
}
