using Riok.Mapperly.Abstractions;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

[Mapper]
public static partial class ListingMapper
{
    public static partial ListingBriefDto ToBrief(Listing listing);

    public static partial List<ListingBriefDto> ToBriefList(List<Listing> listings);
}