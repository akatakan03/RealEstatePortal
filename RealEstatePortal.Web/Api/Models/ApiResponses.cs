namespace RealEstatePortal.Web.Api.Models;

public record ListingSummaryResponse(
    int Id, string Title, string Slug,
    decimal Price, string Currency,
    string ListingType, string PropertyType,
    int Bedrooms, decimal AreaSqMeters,
    double? Latitude, double? Longitude,
    string? CoverImageUrl);

public record ListingDetailResponse(
    int Id, string Title, string Slug, string Description,
    decimal Price, string Currency,
    string ListingType, string PropertyType,
    int Bedrooms, int Bathrooms, decimal AreaSqMeters,
    string Address, double? Latitude, double? Longitude,
    IReadOnlyList<string> ImageUrls);

public record PagedResponse<T>(
    IReadOnlyList<T> Items, int PageNumber, int TotalPages, int TotalCount);