using MediatR;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Commands.UpdateListing;

public class UpdateListingCommand : IRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public ListingType ListingType { get; set; } = ListingType.Sale;
    public PropertyType PropertyType { get; set; } = PropertyType.Apartment;
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public decimal AreaSqMeters { get; set; }
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public HeatingType? Heating { get; set; }
    public InternetInfrastructure? Internet { get; set; }
    public bool IsFurnished { get; set; }
    public bool HasBalcony { get; set; }
    public bool HasParking { get; set; }
    public int? FloorNumber { get; set; }
    public int? TotalFloors { get; set; }
    public int? BuildingAge { get; set; }
    public decimal? MonthlyDues { get; set; }
}