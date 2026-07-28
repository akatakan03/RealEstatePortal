namespace RealEstatePortal.Infrastructure.Neighborhood;

// Bound from the "Overpass" configuration section. Overpass is the free OpenStreetMap query API;
// no key is needed. Several public instances exist and any one of them can be busy or down, so a
// list is tried in order until one answers — the endpoints are configurable so ops can reorder or
// replace them without a code change.
public class OverpassSettings
{
    // Per-endpoint attempt budget. Kept short so a stalled instance is abandoned quickly and the
    // next one is tried, rather than blocking the whole request on one slow server.
    public int PerEndpointTimeoutSeconds { get; set; } = 8;

    public List<string> Endpoints { get; set; } = new()
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter"
    };
}
