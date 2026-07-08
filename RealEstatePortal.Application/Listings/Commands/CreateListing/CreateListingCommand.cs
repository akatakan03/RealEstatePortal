using MediatR;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Commands.CreateListing;

public class CreateListingCommand : IRequest<int>
{
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
}