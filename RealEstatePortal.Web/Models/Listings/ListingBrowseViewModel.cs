using System.Globalization;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;

namespace RealEstatePortal.Web.Models.Listings;

public class ListingBrowseViewModel
{
    public PaginatedList<ListingBriefDto> Listings { get; set; } = default!;
    public GetPublicListingsQuery Filter { get; set; } = new();

    // Current filters as route values, so pager links preserve them
    public Dictionary<string, string> CurrentFilter()
    {
        var d = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(Filter.Keyword)) d["Keyword"] = Filter.Keyword;
        if (Filter.ListingType.HasValue) d["ListingType"] = Filter.ListingType.ToString()!;
        if (Filter.PropertyType.HasValue) d["PropertyType"] = Filter.PropertyType.ToString()!;
        if (Filter.MinPrice.HasValue) d["MinPrice"] = Filter.MinPrice.Value.ToString(CultureInfo.InvariantCulture);
        if (Filter.MaxPrice.HasValue) d["MaxPrice"] = Filter.MaxPrice.Value.ToString(CultureInfo.InvariantCulture);
        if (Filter.MinBedrooms.HasValue) d["MinBedrooms"] = Filter.MinBedrooms.Value.ToString();
        if (Filter.Heating.HasValue) d["Heating"] = Filter.Heating.ToString();
        if (Filter.Internet.HasValue) d["Internet"] = Filter.Internet.ToString();
        if (Filter.Furnished == true) d["Furnished"] = "true";
        if (Filter.Parking == true) d["Parking"] = "true";
        if (Filter.Balcony == true) d["Balcony"] = "true";
        if (Filter.MaxDues.HasValue) d["MaxDues"] = Filter.MaxDues.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return d;
    }
}