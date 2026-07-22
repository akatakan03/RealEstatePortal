using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Listing> Listings { get; }
    DbSet<ListingMedia> ListingMedia { get; }
    DbSet<Inquiry> Inquiries { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<SavedSearch> SavedSearches { get; }
    DbSet<ListingView> ListingViews { get; }
    DbSet<ListingViewDaily> ListingViewDailies { get; }
    DbSet<ListingPriceChange> ListingPriceChanges { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}