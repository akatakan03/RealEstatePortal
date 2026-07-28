using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Common.Models;

// The structured filter an LLM extracts from a free-text search sentence. Every field is optional:
// the model fills only what the sentence actually specifies, and leaves the rest null. LocationText
// is a place name (a district or neighbourhood) that the handler geocodes into a map centre;
// UnmatchedCriteria carries phrases the schema can't express (e.g. "ebeveyn banyolu"), so the UI can
// tell the user honestly what wasn't applied rather than silently dropping it.
public record ParsedSearchFilter
{
    public ListingType? ListingType { get; init; }
    public PropertyType? PropertyType { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBedrooms { get; init; }
    public HeatingType? Heating { get; init; }
    public InternetInfrastructure? Internet { get; init; }
    public bool? Furnished { get; init; }
    public bool? Parking { get; init; }
    public bool? Balcony { get; init; }
    public decimal? MaxDues { get; init; }
    public string? LocationText { get; init; }
    public string? Keyword { get; init; }
    public IReadOnlyList<string> UnmatchedCriteria { get; init; } = Array.Empty<string>();
}
