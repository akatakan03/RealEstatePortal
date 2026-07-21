namespace RealEstatePortal.Web.Helpers;

public static class ListingImages
{
    // Returns the real cover if there is one; otherwise, when demo placeholders are enabled,
    // a deterministic realistic stock photo (same listing id -> same photo). Never touches data.
    public static string? CoverOrPlaceholder(string? coverUrl, int listingId, bool usePlaceholder)
    {
        if (!string.IsNullOrEmpty(coverUrl))
            return coverUrl;

        // LoremFlickr filters by keyword tags (house/apartment); ?lock pins one stable image
        // per listing so it doesn't change on every reload.
        return usePlaceholder
            ? $"https://loremflickr.com/600/400/house,apartment?lock={listingId}"
            : null;
    }
}
