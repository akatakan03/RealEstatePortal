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

    // Stable per-listing seeds for a demo gallery. The first seed is the listing id itself,
    // so the detail gallery's opening photo matches the card's cover.
    public static IReadOnlyList<int> PlaceholderSeeds(int listingId, int count)
    {
        var seeds = new List<int>(count);
        for (var i = 0; i < count; i++)
            seeds.Add(i == 0 ? listingId : listingId * 100 + i);
        return seeds;
    }
}
