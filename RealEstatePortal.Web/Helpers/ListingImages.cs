namespace RealEstatePortal.Web.Helpers;

public static class ListingImages
{
    // Returns the real cover if there is one; otherwise, when demo placeholders are enabled,
    // a deterministic realistic stock photo (same listing id -> same photo). Never touches data.
    public static string? CoverOrPlaceholder(string? coverUrl, int listingId, bool usePlaceholder)
    {
        if (!string.IsNullOrEmpty(coverUrl))
            return coverUrl;

        return usePlaceholder
            ? $"https://picsum.photos/seed/listing{listingId}/600/400"
            : null;
    }
}
