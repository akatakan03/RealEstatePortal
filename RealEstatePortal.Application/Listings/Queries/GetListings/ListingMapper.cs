using Riok.Mapperly.Abstractions;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

[Mapper]
public static partial class ListingMapper
{
    [MapperIgnoreTarget(nameof(ListingBriefDto.CoverThumbnailKey))]
    [MapperIgnoreTarget(nameof(ListingBriefDto.CoverThumbnailUrl))]
    public static partial ListingBriefDto ToBrief(Listing listing);

    public static partial List<ListingBriefDto> ToBriefList(List<Listing> listings);

    [MapperIgnoreTarget(nameof(ListingDetailDto.ImageUrls))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.Latitude))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.Longitude))]
    public static partial ListingDetailDto ToDetail(Listing listing);
}