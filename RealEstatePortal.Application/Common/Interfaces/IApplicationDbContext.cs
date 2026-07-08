namespace RealEstatePortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // DbSet<T> properties will be added here as entities are created, e.g.:
    // DbSet<Listing> Listings { get; }
}