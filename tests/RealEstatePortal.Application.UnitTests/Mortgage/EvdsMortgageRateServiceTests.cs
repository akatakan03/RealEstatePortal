using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RealEstatePortal.Infrastructure.Mortgage;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Mortgage;

public class EvdsMortgageRateServiceTests
{
    // Records what it was called with and replays a fixed response, so a test can assert on the
    // request (key header, call count) and control the payload/status.
    private class EvdsStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public int Calls { get; private set; }
        public string? LastKeyHeader { get; private set; }

        public EvdsStubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastKeyHeader = request.Headers.TryGetValues("key", out var v) ? string.Join(",", v) : null;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private static MortgageSettings Settings(string? apiKey = "test-key") => new()
    {
        DefaultMonthlyRate = 3.0m,
        Evds = new EvdsSettings { ApiKey = apiKey, HousingLoanSeries = "TP.KTF12" }
    };

    private static EvdsMortgageRateService Build(
        EvdsStubHandler handler, MortgageSettings settings, IMemoryCache? cache = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://evds.test/service/evds/") };
        return new EvdsMortgageRateService(
            client,
            Options.Create(settings),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILogger<EvdsMortgageRateService>>());
    }

    [Fact]
    public async Task ConvertsTheAnnualSeriesValueToAMonthlyRate()
    {
        // Annual 42.0 / 12 = 3.5 monthly.
        var handler = new EvdsStubHandler(
            "{\"items\":[{\"Tarih\":\"01-01-2024\",\"TP_KTF12\":\"42.0\"}]}");
        var service = Build(handler, Settings());

        var rate = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        rate.ShouldBe(3.5m);
        handler.LastKeyHeader.ShouldBe("test-key");   // the API key travels as the 'key' header
    }

    [Fact]
    public async Task TakesTheMostRecentNonNullPoint()
    {
        // Newest point (last) hasn't been published yet (null) — fall back to the latest real one.
        var handler = new EvdsStubHandler(
            "{\"items\":[{\"TP_KTF12\":\"36.0\"},{\"TP_KTF12\":\"48.0\"},{\"TP_KTF12\":null}]}");
        var service = Build(handler, Settings());

        var rate = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        rate.ShouldBe(4.0m);   // 48.0 / 12, the newest value that actually has a number
    }

    [Fact]
    public async Task UsesFallback_AndSkipsHttp_WhenNoApiKey()
    {
        var handler = new EvdsStubHandler("{\"items\":[{\"TP_KTF12\":\"42.0\"}]}");
        var service = Build(handler, Settings(apiKey: ""));

        var rate = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        rate.ShouldBe(3.0m);          // the configured fallback
        handler.Calls.ShouldBe(0);    // never touched the network
    }

    [Fact]
    public async Task UsesFallback_OnHttpError()
    {
        var handler = new EvdsStubHandler("nope", HttpStatusCode.InternalServerError);
        var service = Build(handler, Settings());

        var rate = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        rate.ShouldBe(3.0m);
    }

    [Fact]
    public async Task UsesFallback_WhenSeriesHasNoData()
    {
        var handler = new EvdsStubHandler("{\"items\":[]}");
        var service = Build(handler, Settings());

        var rate = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        rate.ShouldBe(3.0m);
    }

    [Fact]
    public async Task CachesTheResult_SoASecondCallDoesNotHitEvds()
    {
        var handler = new EvdsStubHandler("{\"items\":[{\"TP_KTF12\":\"42.0\"}]}");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Build(handler, Settings(), cache);

        var first = await service.GetMonthlyRatePercentAsync(CancellationToken.None);
        var second = await service.GetMonthlyRatePercentAsync(CancellationToken.None);

        first.ShouldBe(3.5m);
        second.ShouldBe(3.5m);
        handler.Calls.ShouldBe(1);   // the second read came from cache
    }
}
