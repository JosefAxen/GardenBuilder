namespace GardenBuilder.Contracts.Gardens;

/// <summary>The customer's gardens, for modules that need one, such as Design.</summary>
public interface IGardens
{
    /// <summary>Creates a garden for the customer and returns its id.</summary>
    Task<Guid> CreateAsync(Guid customerId, CancellationToken cancellationToken);
}
