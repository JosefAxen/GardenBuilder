namespace GardenBuilder.Contracts.Identity;

/// <summary>
/// The anonymous customer behind the current request, recognised by a secure cookie (docs/superpowers/specs/
/// 2026-11-16-identity-design.md). Customers have no name or e-mail yet; the cookie is all that ties them to their data.
/// </summary>
/// <remarks>
/// Each call to <see cref="GetOrCreateAsync"/> on a request without a valid cookie creates a new customer, and the
/// browser keeps the cookie it received last. A client must therefore save its first things one at a time, not in
/// parallel, or the earlier customer and what it saved are left behind.
/// </remarks>
public interface ICurrentCustomer
{
    /// <summary>The customer's id, or null when the request has no valid cookie or the customer no longer exists. Never creates one.</summary>
    Task<Guid?> FindAsync(CancellationToken cancellationToken);

    /// <summary>The customer's id, creating the customer and setting the cookie when there is none. Call it only when something is saved.</summary>
    Task<Guid> GetOrCreateAsync(CancellationToken cancellationToken);
}
