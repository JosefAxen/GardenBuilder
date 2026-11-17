using GardenBuilder.Contracts;
using GardenBuilder.Contracts.Database;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Domain;
using GardenBuilder.Modules.Identity.Features.CurrentCustomer;
using GardenBuilder.Modules.Identity.Features.StaffAccounts;
using GardenBuilder.Modules.Identity.Features.StaffLogin;
using GardenBuilder.Modules.Identity.Features.WhoAmI;
using GardenBuilder.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Identity;

/// <summary>Kunder, personal, roller och samtycken.</summary>
public static class IdentityModule
{
    public const string Schema = "identity";

    /// <summary>The authentication scheme that recognises an anonymous customer.</summary>
    public const string CustomerScheme = "Customer";

    public const string CustomerCookieName = "gb-customer";

    /// <summary>Signed-in staff (Säljare or Administratör) who have passed two-factor.</summary>
    public const string StaffPolicy = StaffPolicies.Staff;

    /// <summary>Signed-in Administratör.</summary>
    public const string AdminPolicy = StaffPolicies.Admin;

    /// <summary>How long a customer is remembered without a visit (docs/design.md: designs are deleted after 90 days of inactivity).</summary>
    public static readonly TimeSpan CustomerCookieLifetime = TimeSpan.FromDays(90);

    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<IdentityDbContext>((services, options) => options.UseNpgsql(
            services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.Database),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema))
            .UseSnakeCaseNamingConvention());
        builder.EnrichNpgsqlDbContext<IdentityDbContext>();
        builder.Services.AddScoped<IModuleMigrator, IdentityMigrator>();

        // Cookies are encrypted with keys kept in the database, so they survive restarts and work on every instance.
        builder.Services.AddDataProtection().SetApplicationName("GardenBuilder").PersistKeysToDbContext<IdentityDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthorization();
        var authentication = builder.Services.AddAuthentication();
        authentication.AddCookie(CustomerScheme, options =>
        {
            options.Cookie.Name = CustomerCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = CustomerCookieLifetime;

            // CurrentCustomer renews the cookie itself once a day; the API never redirects to a login page.
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddScoped<ICurrentCustomer, CurrentCustomer>();

        // Staff: ASP.NET Core Identity with the rules from the identity spec.
        builder.Services.AddIdentityCore<StaffUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<StaffRoleRecord>()
            .AddClaimsPrincipalFactory<StaffClaimsFactory>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager<StaffSignInManager>()
            .AddDefaultTokenProviders();
        builder.Services.AddStaffAuthentication(authentication);
        builder.Services.AddScoped<IStaffAccounts, StaffAccounts>();

        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");
        group.MapWhoAmI();
        group.MapGroup("/staff")
            .MapLogin()
            .MapChangePassword()
            .MapSetUpTwoFactor()
            .MapVerifyTwoFactor()
            .MapLogout();

        return app;
    }
}
