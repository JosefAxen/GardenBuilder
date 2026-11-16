using System.Text.Json.Serialization;
using GardenBuilder.Contracts.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.WhoAmI;

/// <summary>Who the browser is: staff, a customer, or nobody. The web asks this instead of storing anything itself.</summary>
internal static class WhoAmI
{
    /// <param name="Kind">"none", "customer" or "staff".</param>
    /// <param name="Role">For staff: "sales" or "admin".</param>
    public sealed record MeResponse(
        string Kind,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role = null);

    public static RouteGroupBuilder MapWhoAmI(this RouteGroupBuilder group)
    {
        group.MapGet("/me", HandleAsync).WithName("WhoAmI");
        return group;
    }

    private static async Task<Ok<MeResponse>> HandleAsync(ICurrentCustomer customer, CancellationToken cancellationToken) =>
        TypedResults.Ok(await customer.FindAsync(cancellationToken) is null ? new MeResponse("none") : new MeResponse("customer"));
}
