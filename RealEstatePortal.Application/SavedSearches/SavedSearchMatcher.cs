using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.SavedSearches;

public static class SavedSearchMatcher
{
    public static bool Matches(SavedSearch search, Listing listing)
    {
        if (search.ListingType.HasValue && listing.ListingType != search.ListingType.Value)
            return false;

        if (search.PropertyType.HasValue && listing.PropertyType != search.PropertyType.Value)
            return false;

        if (search.MaxPrice.HasValue && listing.Price.Amount > search.MaxPrice.Value)
            return false;

        if (search.MinBedrooms.HasValue && listing.Bedrooms < search.MinBedrooms.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var kw = search.Keyword.Trim();
            var inTitle = listing.Title.Contains(kw, StringComparison.OrdinalIgnoreCase);
            var inAddress = listing.Address.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (!inTitle && !inAddress)
                return false;
        }

        return true;   // passed every specified criterion
    }
}