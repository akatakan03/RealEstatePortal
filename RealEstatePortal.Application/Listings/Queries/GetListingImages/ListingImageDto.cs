namespace RealEstatePortal.Application.Listings.Queries.GetListingImages;

public class ListingImageDto
{
    public int Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public bool IsCover { get; init; }
    public int Order { get; init; }
}