using System.Security.Claims;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Domain;
using GardenBuilder.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Identity.Features.CurrentCustomer;

/// <summary>Reads and writes the customer cookie. The cookie holds only the customer's id, encrypted by ASP.NET Core.</summary>
internal sealed class CurrentCustomer(IHttpContextAccessor accessor, IdentityDbContext db, TimeProvider time) : ICurrentCustomer
{
    private const string IdClaim = "customer_id";

    public async Task<Guid?> FindAsync(CancellationToken cancellationToken)
    {
        if (accessor.HttpContext is not { } http)
        {
            return null;
        }

        var result = await http.AuthenticateAsync(IdentityModule.CustomerScheme);
        if (!result.Succeeded || !Guid.TryParse(result.Principal.FindFirstValue(IdClaim), out var id))
        {
            return null;
        }

        // A valid cookie for a customer that has been deleted counts as no cookie.
        var customer = await db.Set<Customer>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        // Once a day: note the visit and give the cookie another 90 days, so only 90 days without a visit end it.
        if (customer.Seen(time.GetUtcNow()))
        {
            await db.SaveChangesAsync(cancellationToken);
            await SignInAsync(http, customer.Id);
        }

        return customer.Id;
    }

    public async Task<Guid> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        if (await FindAsync(cancellationToken) is { } existing)
        {
            return existing;
        }

        var http = accessor.HttpContext
            ?? throw new InvalidOperationException("A customer can only be created while handling a request.");
        var customer = Customer.Create(time.GetUtcNow());
        db.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        await SignInAsync(http, customer.Id);
        return customer.Id;
    }

    private static Task SignInAsync(HttpContext http, Guid id)
    {
        var identity = new ClaimsIdentity([new Claim(IdClaim, id.ToString())], IdentityModule.CustomerScheme);
        return http.SignInAsync(IdentityModule.CustomerScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }
}
