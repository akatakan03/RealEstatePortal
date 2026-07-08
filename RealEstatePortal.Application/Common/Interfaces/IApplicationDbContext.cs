using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Listing> Listings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}