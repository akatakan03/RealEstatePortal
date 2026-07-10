using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Infrastructure.Geocoding;

public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(HttpClient httpClient, ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GeoCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        foreach (var candidate in BuildCandidates(address))
        {
            var coord = await TryGeocodeAsync(candidate, cancellationToken);
            if (coord is not null)
            {
                if (!string.Equals(candidate, address, StringComparison.Ordinal))
                    _logger.LogInformation("Geocoded via fallback \"{Candidate}\" for address \"{Address}\"", candidate, address);
                return coord;
            }
        }

        _logger.LogWarning("Geocoding found no match for address: {Address}", address);
        return null;
    }

    // Full address first, then progressively broader versions.
    private static IEnumerable<string> BuildCandidates(string address)
    {
        yield return address;

        // Split into comma-separated parts, trimming common TR abbreviations.
        var parts = address
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.EndsWith("Mh.", StringComparison.OrdinalIgnoreCase)
                     && !p.EndsWith("Cd.", StringComparison.OrdinalIgnoreCase)
                     && !p.EndsWith("Sk.", StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("No", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Try the cleaned full set, then the last 3, then the last 2 parts.
        if (parts.Count > 0)
        {
            var cleaned = string.Join(", ", parts);
            yield return cleaned;

            if (parts.Count > 3) yield return string.Join(", ", parts.TakeLast(3));
            if (parts.Count > 2) yield return string.Join(", ", parts.TakeLast(2));
        }
    }

    private async Task<GeoCoordinate?> TryGeocodeAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"search?q={Uri.EscapeDataString(address)}&format=json&limit=1";
            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);

            var first = results?.FirstOrDefault();
            if (first is null) return null;

            if (double.TryParse(first.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(first.Lon, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
            {
                return new GeoCoordinate(lat, lon);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding request failed for: {Address}", address);
            return null;
        }
    }

    private class NominatimResult
    {
        [JsonPropertyName("lat")] public string Lat { get; set; } = string.Empty;
        [JsonPropertyName("lon")] public string Lon { get; set; } = string.Empty;
    }
}