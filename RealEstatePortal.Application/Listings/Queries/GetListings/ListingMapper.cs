using Riok.Mapperly.Abstractions;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

// Entity -> subset DTOs, so the source deliberately has members with no target.
// RequiredMappingStrategy.Target keeps warnings for genuinely unmapped *target* members
// (a real bug) while silencing the expected unmapped-source noise.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ListingMapper
{
    // These targets are filled outside the mapper (favorite status, coordinates from the
    // owned Location, cover image URL), so the mapper intentionally leaves them alone.
    [MapperIgnoreTarget(nameof(ListingBriefDto.CoverThumbnailKey))]
    [MapperIgnoreTarget(nameof(ListingBriefDto.CoverThumbnailUrl))]
    [MapperIgnoreTarget(nameof(ListingBriefDto.IsFavorited))]
    [MapperIgnoreTarget(nameof(ListingBriefDto.Latitude))]
    [MapperIgnoreTarget(nameof(ListingBriefDto.Longitude))]
    public static partial ListingBriefDto ToBrief(Listing listing);

    public static partial List<ListingBriefDto> ToBriefList(List<Listing> listings);

    // Owner display fields and image URLs are resolved separately after mapping.
    [MapperIgnoreTarget(nameof(ListingDetailDto.ImageUrls))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.Latitude))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.Longitude))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.OwnerName))]
    [MapperIgnoreTarget(nameof(ListingDetailDto.OwnerAvatarUrl))]
    public static partial ListingDetailDto ToDetail(Listing listing);
}