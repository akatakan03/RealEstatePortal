using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Web.Api.Models;

public record CreateListingRequest(
    string Title, string Description,
    decimal Price, string Currency,
    ListingType ListingType, PropertyType PropertyType,
    int Bedrooms, int Bathrooms, decimal AreaSqMeters,
    string Address, double? Latitude, double? Longitude);

public record UpdateListingRequest(
    string Title, string Description,
    decimal Price, string Currency,
    ListingType ListingType, PropertyType PropertyType,
    int Bedrooms, int Bathrooms, decimal AreaSqMeters,
    string Address, double? Latitude, double? Longitude);