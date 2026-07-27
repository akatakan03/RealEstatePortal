using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Infrastructure.Mortgage;

// Seeds the loan calculator with the sector-average housing-loan rate from TCMB EVDS. The result
// is cached (EVDS refreshes weekly), and every failure path — no key, HTTP error, empty series —
// falls back to the configured default so the calculator always has a sensible number.
public class EvdsMortgageRateService : IMortgageRateService
{
    private const string CacheKey = "mortgage:monthly-rate";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    private readonly HttpClient _http;
    private readonly MortgageSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EvdsMortgageRateService> _logger;

    public EvdsMortgageRateService(
        HttpClient http,
        IOptions<MortgageSettings> settings,
        IMemoryCache cache,
        ILogger<EvdsMortgageRateService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetMonthlyRatePercentAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<decimal>(CacheKey, out var cached))
            return cached;

        var rate = await TryFetchMonthlyRateAsync(cancellationToken) ?? _settings.DefaultMonthlyRate;

        // Cache the fallback too: if EVDS is unreachable we don't want to hammer it on every
        // page view for the next 12 hours either.
        _cache.Set(CacheKey, rate, CacheDuration);
        return rate;
    }

    private async Task<decimal?> TryFetchMonthlyRateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Evds.ApiKey))
            return null;   // not configured — use the fallback silently

        try
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-90);   // a window wide enough to always contain a weekly point
            var series = _settings.Evds.HousingLoanSeries;

            var url = $"?series={Uri.EscapeDataString(series)}" +
                      $"&startDate={start:dd-MM-yyyy}&endDate={end:dd-MM-yyyy}&type=json";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("key", _settings.Evds.ApiKey);

            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<EvdsResponse>(cancellationToken: cancellationToken);
            var annual = LatestValue(payload, series);
            if (annual is null || annual <= 0)
            {
                _logger.LogWarning("EVDS returned no usable value for series {Series}; using fallback.", series);
                return null;
            }

            // EVDS reports an annual rate; Turkish mortgages apply a monthly rate quoted as
            // annual / 12 (nominal), which is the number the calculator's monthly formula wants.
            return Math.Round(annual.Value / 12m, 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EVDS mortgage rate fetch failed; using fallback.");
            return null;
        }
    }

    // The value column is named after the series with dots turned into underscores
    // (TP.KTF12 -> TP_KTF12). Values arrive as strings and some points may be null, so we take
    // the most recent parseable one.
    private static decimal? LatestValue(EvdsResponse? payload, string series)
    {
        if (payload?.Items is null)
            return null;

        var column = series.Replace('.', '_');
        decimal? latest = null;

        foreach (var item in payload.Items)
        {
            if (item.TryGetValue(column, out var element)
                && element.ValueKind == JsonValueKind.String
                && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                latest = value;   // keep overwriting so we end on the newest valid point
            }
        }

        return latest;
    }

    private class EvdsResponse
    {
        [JsonPropertyName("items")]
        public List<Dictionary<string, JsonElement>>? Items { get; set; }
    }
}
