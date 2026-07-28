using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Infrastructure.Neighborhood;

// Counts nearby amenities via the OpenStreetMap Overpass API. Raw OSM features are classified into
// a handful of stable categories and aggregated to counts + nearest distance. The public Overpass
// instances are rate-limited and often busy, so several are tried in order; if every one fails the
// result is an empty list, and the neighborhood card simply omits amenities rather than breaking.
public class OverpassPoiService : INeighborhoodPoiService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OverpassPoiService> _logger;
    private readonly OverpassSettings _settings;

    // OSM data barely moves; the same corner asked twice in a day should not hit Overpass twice.
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    public OverpassPoiService(
        HttpClient http, IMemoryCache cache, IOptions<OverpassSettings> settings,
        ILogger<OverpassPoiService> logger)
    {
        _http = http;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NeighborhoodPoi>> GetNearbyAsync(
        double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
    {
        // Round the key to ~110 m so nearby listings share a cache entry; the answer is a
        // neighborhood summary, not a per-metre reading.
        var key = $"overpass:{lat:F3}:{lng:F3}:{radiusMeters}";
        if (_cache.TryGetValue(key, out IReadOnlyList<NeighborhoodPoi>? cached) && cached is not null)
            return cached;

        var query = BuildQuery(lat, lng, radiusMeters);

        // Race all instances at once and take the first that answers, rather than trying them one
        // after another — a single dead instance would otherwise add its whole timeout to the wait.
        // One shared deadline bounds the total, and a win cancels the stragglers. Uncached lookups
        // are rare (24 h cache per ~110 m cell), so briefly querying a few public mirrors is fine.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.PerEndpointTimeoutSeconds)));

        var pending = _settings.Endpoints
            .Select(endpoint => (endpoint, task: QueryAsync(endpoint, query, lat, lng, deadline.Token)))
            .ToList();

        try
        {
            while (pending.Count > 0)
            {
                var finished = await Task.WhenAny(pending.Select(p => p.task));
                var winner = pending.First(p => p.task == finished);
                pending.Remove(winner);

                try
                {
                    var result = await finished;
                    deadline.Cancel(); // stop the other in-flight requests
                    _cache.Set(key, result, CacheFor);
                    return result;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeouts, 429s and 504s from the shared public endpoints all land here. Not
                    // fatal — wait on the next one that finishes, or degrade if they all fail.
                    _logger.LogWarning(ex, "Overpass endpoint {Endpoint} failed for {Lat},{Lng}",
                        winner.endpoint, lat, lng);
                }
            }
        }
        finally
        {
            // Cancel and observe any stragglers so their cancellation doesn't surface as an
            // unobserved task exception.
            deadline.Cancel();
            _ = Task.WhenAll(pending.Select(p => p.task)).ContinueWith(
                t => { _ = t.Exception; }, TaskScheduler.Default);
        }

        return Array.Empty<NeighborhoodPoi>();
    }

    private async Task<IReadOnlyList<NeighborhoodPoi>> QueryAsync(
        string endpoint, string query, double lat, double lng, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("data", query)
        });

        using var response = await _http.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return Array.Empty<NeighborhoodPoi>();

        // category -> (count, nearest distance seen)
        var agg = new Dictionary<string, (int Count, int? Nearest)>();

        foreach (var el in elements.EnumerateArray())
        {
            var category = Classify(el);
            if (category is null)
                continue;

            var distance = DistanceMeters(lat, lng, el);

            if (!agg.TryGetValue(category, out var current))
                current = (0, null);

            current.Count++;
            if (distance is not null && (current.Nearest is null || distance < current.Nearest))
                current.Nearest = distance;

            agg[category] = current;
        }

        return agg
            .Select(kv => new NeighborhoodPoi(kv.Key, kv.Value.Count, kv.Value.Nearest))
            .OrderBy(p => p.Category)
            .ToList();
    }

    // One Overpass query, node/way/relation, unioning the tag sets we care about. `out center`
    // gives ways/relations a representative coordinate so distances can be measured for all.
    private static string BuildQuery(double lat, double lng, int radius)
    {
        var la = lat.ToString("F6", CultureInfo.InvariantCulture);
        var ln = lng.ToString("F6", CultureInfo.InvariantCulture);
        var around = $"(around:{radius},{la},{ln})";
        return "[out:json][timeout:20];(" +
            $"nwr{around}[\"amenity\"~\"^(school|kindergarten|university|college)$\"];" +
            $"nwr{around}[\"amenity\"~\"^(hospital|clinic|doctors|pharmacy)$\"];" +
            $"nwr{around}[\"shop\"~\"^(supermarket|convenience|mall)$\"];" +
            $"nwr{around}[\"amenity\"=\"marketplace\"];" +
            $"nwr{around}[\"highway\"=\"bus_stop\"];" +
            $"nwr{around}[\"amenity\"=\"bus_station\"];" +
            $"nwr{around}[\"railway\"~\"^(station|subway_entrance|tram_stop|halt)$\"];" +
            $"nwr{around}[\"public_transport\"~\"^(station|stop_position|platform)$\"];" +
            ");out center tags;";
    }

    // Maps a raw OSM element to one of our stable category keys, or null to ignore it.
    private static string? Classify(JsonElement el)
    {
        if (!el.TryGetProperty("tags", out var tags))
            return null;

        var amenity = Tag(tags, "amenity");
        var shop = Tag(tags, "shop");
        var highway = Tag(tags, "highway");
        var railway = Tag(tags, "railway");
        var publicTransport = Tag(tags, "public_transport");

        if (amenity is "school" or "kindergarten" or "university" or "college")
            return "school";
        if (amenity is "hospital" or "clinic" or "doctors" or "pharmacy")
            return "health";
        if (shop is "supermarket" or "convenience" or "mall" || amenity is "marketplace")
            return "market";
        if (highway == "bus_stop" || amenity == "bus_station"
            || railway is "station" or "subway_entrance" or "tram_stop" or "halt"
            || publicTransport is "station" or "stop_position" or "platform")
            return "transit";

        return null;
    }

    private static string? Tag(JsonElement tags, string name) =>
        tags.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    // Element coordinate: nodes carry lat/lon directly; ways/relations carry a "center".
    private static int? DistanceMeters(double lat, double lng, JsonElement el)
    {
        double? elLat = null, elLng = null;

        if (el.TryGetProperty("lat", out var latProp) && el.TryGetProperty("lon", out var lonProp))
        {
            elLat = latProp.GetDouble();
            elLng = lonProp.GetDouble();
        }
        else if (el.TryGetProperty("center", out var center)
            && center.TryGetProperty("lat", out var clat)
            && center.TryGetProperty("lon", out var clon))
        {
            elLat = clat.GetDouble();
            elLng = clon.GetDouble();
        }

        if (elLat is null || elLng is null)
            return null;

        return (int)Math.Round(Haversine(lat, lng, elLat.Value, elLng.Value));
    }

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadius = 6371000; // metres
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
            * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
