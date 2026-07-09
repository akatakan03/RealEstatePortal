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
            _logger.LogWarning(ex, "Geocoding failed for address: {Address}", address);
            return null;
        }
    }

    private class NominatimResult
    {
        [JsonPropertyName("lat")] public string Lat { get; set; } = string.Empty;
        [JsonPropertyName("lon")] public string Lon { get; set; } = string.Empty;
    }
}