using Riok.Mapperly.Abstractions;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

[Mapper]
public static partial class ListingMapper
{
    public static partial ListingBriefDto ToBrief(Listing listing);

    public static partial List<ListingBriefDto> ToBriefList(List<Listing> listings);

    // Expression-based projection — translates to SQL SELECT, no in-memory mapping
    public static partial IQueryable<ListingBriefDto> ProjectToBrief(this IQueryable<Listing> source);

    public static partial ListingDetailDto ToDetail(Listing listing);
}