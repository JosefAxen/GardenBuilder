using GardenBuilder.Contracts;
using GardenBuilder.Modules.Leads.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Leads;

/// <summary>Offertförfrågningar, ärendestatus och offerter.</summary>
public static class LeadsModule
{
    public const string Schema = "leads";

    public static IHostApplicationBuilder AddLeadsModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<LeadsDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema)));
        builder.EnrichNpgsqlDbContext<LeadsDbContext>();

        return builder;
    }

    public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leads").WithTags("Leads");
        _ = group; // Map features here, e.g. group.MapSomething();

        return app;
    }
}
