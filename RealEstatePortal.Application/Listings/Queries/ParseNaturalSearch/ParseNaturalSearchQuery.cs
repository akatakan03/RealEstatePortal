using MediatR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;

namespace RealEstatePortal.Application.Listings.Queries.ParseNaturalSearch;

// Turns a natural-language sentence into a ready-to-run public-listings filter. The controller runs
// the returned Filter through the normal GetPublicListings pipeline, so AI search reuses every
// existing filter, sort, and pagination path — it only changes how the filter is *populated*.
public record ParseNaturalSearchQuery(string Text) : IRequest<NaturalSearchResult>;

// AiApplied is false when the parser was unavailable or failed and we fell back to a plain keyword
// search — the UI uses it to avoid claiming "we understood your sentence" when we didn't.
public record NaturalSearchResult(
    GetPublicListingsQuery Filter,
    IReadOnlyList<string> UnmatchedCriteria,
    bool AiApplied);

public class ParseNaturalSearchQueryHandler
    : IRequestHandler<ParseNaturalSearchQuery, NaturalSearchResult>
{
    // A district/neighbourhood centre is a broad hint, not a pin — search a wide radius around it.
    private const double LocationRadiusKm = 6;

    private readonly INaturalLanguageSearchParser _parser;
    private readonly IGeocodingService _geocoding;

    public ParseNaturalSearchQueryHandler(
        INaturalLanguageSearchParser parser, IGeocodingService geocoding)
    {
        _parser = parser;
        _geocoding = geocoding;
    }

    public async Task<NaturalSearchResult> Handle(
        ParseNaturalSearchQuery request, CancellationToken cancellationToken)
    {
        var parsed = await _parser.ParseAsync(request.Text, cancellationToken);

        // Parser off or failed — degrade to the literal sentence as a keyword search.
        if (parsed is null)
            return new NaturalSearchResult(
                new GetPublicListingsQuery { Keyword = request.Text.Trim() },
                Array.Empty<string>(),
                AiApplied: false);

        var filter = new GetPublicListingsQuery
        {
            Keyword = string.IsNullOrWhiteSpace(parsed.Keyword) ? null : parsed.Keyword.Trim(),
            ListingType = parsed.ListingType,
            PropertyType = parsed.PropertyType,
            MinPrice = parsed.MinPrice,
            MaxPrice = parsed.MaxPrice,
            MinBedrooms = parsed.MinBedrooms,
            Heating = parsed.Heating,
            Internet = parsed.Internet,
            Furnished = parsed.Furnished,
            Parking = parsed.Parking,
            Balcony = parsed.Balcony,
            MaxDues = parsed.MaxDues
        };

        // Resolve a place name to a map centre so "Kadıköy" becomes a spatial search, reusing the
        // same geocoder (and cache) the create/edit forms use.
        if (!string.IsNullOrWhiteSpace(parsed.LocationText))
        {
            var coord = await _geocoding.GeocodeAsync(parsed.LocationText, cancellationToken);
            if (coord is not null)
            {
                filter.CenterLat = coord.Latitude;
                filter.CenterLng = coord.Longitude;
                filter.RadiusKm = LocationRadiusKm;
            }
            else if (filter.Keyword is null)
            {
                // Couldn't place it on the map — keep it as a text match against title/address.
                filter.Keyword = parsed.LocationText.Trim();
            }
        }

        return new NaturalSearchResult(filter, parsed.UnmatchedCriteria, AiApplied: true);
    }
}
