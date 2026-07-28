using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Infrastructure.Search;

// Parses a Turkish real-estate search sentence into a structured filter with Google Gemini. The
// sentence is passed as data and the response is constrained to a JSON schema (Gemini's structured
// output), so the model can only ever return our filter shape — it cannot emit free-form actions.
// Every failure path (no key, HTTP error, unparseable body) returns null, and the caller degrades
// to a keyword search, so the site never depends on this being configured or reachable.
public class GeminiNaturalLanguageSearchParser : INaturalLanguageSearchParser
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // The instruction is stable, so the enum semantics that map Turkish phrasing to our domain
    // values live here rather than being rebuilt per request.
    private const string SystemInstruction = """
        You extract structured search filters from a Turkish real-estate search sentence for a
        listings site in İstanbul. Fill only the fields the sentence actually specifies; leave
        everything else null. Never invent values.

        Field guidance:
        - listingType: "Sale" for satılık, "Rent" for kiralık.
        - propertyType: Apartment (daire), House (müstakil ev/villa), Land (arsa), Commercial (işyeri/dükkan).
        - minBedrooms: Turkish room counts like "3+1" mean 3 bedrooms → 3; "2+1" → 2; "stüdyo" → 0.
        - heating: NaturalGas (doğalgaz/kombi), CentralHeating (merkezi), Stove (soba),
          UnderfloorHeating (yerden ısıtma), AirConditioning (klima), None (ısıtmasız).
        - internet: Fiber, VDSL, ADSL (or None).
        - furnished (eşyalı), balcony (balkonlu), parking (otopark/garaj): true only when asked for.
        - minPrice/maxPrice: numbers in Turkish Lira. "5 milyona kadar" → maxPrice 5000000.
          "en az 2 milyon" → minPrice 2000000. maxDues: aidat ceiling in TL.
        - locationText: a district or neighbourhood name only (e.g. "Kadıköy", "Ataşehir Barbaros").
        - keyword: any distinctive free-text the schema can't hold (e.g. a site/complex name).
        - unmatchedCriteria: short phrases you could NOT map to any field, verbatim from the
          sentence (e.g. "ebeveyn banyolu", "metroya yakın", "deniz manzaralı"). This keeps the
          site honest about what it applied.
        """;

    private readonly HttpClient _http;
    private readonly GeminiSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeminiNaturalLanguageSearchParser> _logger;

    public GeminiNaturalLanguageSearchParser(
        HttpClient http,
        IOptions<GeminiSettings> settings,
        IMemoryCache cache,
        ILogger<GeminiNaturalLanguageSearchParser> logger)
    {
        _http = http;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ParsedSearchFilter?> ParseAsync(
        string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(text))
            return null;

        var key = "nlsearch:" + text.Trim().ToLowerInvariant();
        if (_cache.TryGetValue<ParsedSearchFilter>(key, out var cached))
            return cached;

        var parsed = await TryParseAsync(text, cancellationToken);

        // Cache successes only — a transient failure shouldn't stick for 30 minutes.
        if (parsed is not null)
            _cache.Set(key, parsed, CacheDuration);

        return parsed;
    }

    private async Task<ParsedSearchFilter?> TryParseAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                systemInstruction = new { parts = new[] { new { text = SystemInstruction } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = text } } }
                },
                generationConfig = new
                {
                    temperature = 0.0,
                    responseMimeType = "application/json",
                    responseSchema = ResponseSchema
                }
            };

            // Key travels as a query parameter, per the Gemini REST contract.
            var url = $"models/{_settings.Model}:generateContent?key={Uri.EscapeDataString(_settings.ApiKey!)}";
            using var response = await _http.PostAsJsonAsync(url, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini search parse returned {Status}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);
            var json = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var filter = JsonSerializer.Deserialize<GeminiFilter>(json, JsonOptions);
            return filter is null ? null : Map(filter);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Gemini search parse failed");
            return null;
        }
    }

    // The model returns enum members as their string names; parse leniently and drop anything that
    // doesn't match a real value rather than failing the whole search.
    private static ParsedSearchFilter Map(GeminiFilter f) => new()
    {
        ListingType = ParseEnum<ListingType>(f.ListingType),
        PropertyType = ParseEnum<PropertyType>(f.PropertyType),
        MinPrice = Positive(f.MinPrice),
        MaxPrice = Positive(f.MaxPrice),
        MinBedrooms = f.MinBedrooms is >= 0 ? f.MinBedrooms : null,
        Heating = ParseEnum<HeatingType>(f.Heating),
        Internet = ParseEnum<InternetInfrastructure>(f.Internet),
        Furnished = f.Furnished,
        Parking = f.Parking,
        Balcony = f.Balcony,
        MaxDues = Positive(f.MaxDues),
        LocationText = Clean(f.LocationText),
        Keyword = Clean(f.Keyword),
        UnmatchedCriteria = f.UnmatchedCriteria?.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).ToArray() ?? Array.Empty<string>()
    };

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private static decimal? Positive(decimal? value) => value is > 0 ? value : null;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Gemini's structured-output schema (an OpenAPI subset). Enum fields list the exact domain
    // member names so the model can only return values we can map.
    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["listingType"] = new { type = "STRING", @enum = new[] { "Sale", "Rent" }, nullable = true },
            ["propertyType"] = new { type = "STRING", @enum = new[] { "Apartment", "House", "Land", "Commercial" }, nullable = true },
            ["minPrice"] = new { type = "NUMBER", nullable = true },
            ["maxPrice"] = new { type = "NUMBER", nullable = true },
            ["minBedrooms"] = new { type = "INTEGER", nullable = true },
            ["heating"] = new { type = "STRING", @enum = new[] { "None", "Stove", "NaturalGas", "CentralHeating", "UnderfloorHeating", "AirConditioning", "Other" }, nullable = true },
            ["internet"] = new { type = "STRING", @enum = new[] { "Unknown", "None", "ADSL", "VDSL", "Fiber" }, nullable = true },
            ["furnished"] = new { type = "BOOLEAN", nullable = true },
            ["parking"] = new { type = "BOOLEAN", nullable = true },
            ["balcony"] = new { type = "BOOLEAN", nullable = true },
            ["maxDues"] = new { type = "NUMBER", nullable = true },
            ["locationText"] = new { type = "STRING", nullable = true },
            ["keyword"] = new { type = "STRING", nullable = true },
            ["unmatchedCriteria"] = new { type = "ARRAY", items = new { type = "STRING" }, nullable = true }
        }
    };

    // ---- wire DTOs ----

    private class GeminiFilter
    {
        public string? ListingType { get; set; }
        public string? PropertyType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinBedrooms { get; set; }
        public string? Heating { get; set; }
        public string? Internet { get; set; }
        public bool? Furnished { get; set; }
        public bool? Parking { get; set; }
        public bool? Balcony { get; set; }
        public decimal? MaxDues { get; set; }
        public string? LocationText { get; set; }
        public string? Keyword { get; set; }
        public List<string>? UnmatchedCriteria { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
