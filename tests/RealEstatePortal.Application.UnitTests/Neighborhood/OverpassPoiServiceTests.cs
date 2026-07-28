using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RealEstatePortal.Infrastructure.Neighborhood;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Neighborhood;

public class OverpassPoiServiceTests
{
    // Replays a fixed Overpass response and records how many times it was called.
    private class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public int Calls { get; private set; }

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private static OverpassPoiService Build(StubHandler handler, IMemoryCache? cache = null)
    {
        var client = new HttpClient(handler);
        var settings = Options.Create(new OverpassSettings
        {
            Endpoints = new() { "https://overpass.test/api/interpreter" }
        });
        return new OverpassPoiService(
            client,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            settings,
            Substitute.For<ILogger<OverpassPoiService>>());
    }

    // A node ~near the query point (school), a farther node (school), a pharmacy (health), a bus
    // stop (transit), a supermarket way with a center (market), and an untagged node to ignore.
    private const string SampleBody = """
        {"elements":[
          {"type":"node","lat":41.0009,"lon":29.0000,"tags":{"amenity":"school"}},
          {"type":"node","lat":41.0100,"lon":29.0000,"tags":{"amenity":"kindergarten"}},
          {"type":"node","lat":41.0005,"lon":29.0000,"tags":{"amenity":"pharmacy"}},
          {"type":"node","lat":41.0002,"lon":29.0000,"tags":{"highway":"bus_stop"}},
          {"type":"way","center":{"lat":41.0003,"lon":29.0000},"tags":{"shop":"supermarket"}},
          {"type":"node","lat":41.0000,"lon":29.0000,"tags":{"office":"company"}}
        ]}
        """;

    [Fact]
    public async Task GetNearbyAsync_ClassifiesAndCountsByCategory()
    {
        var handler = new StubHandler(SampleBody);
        var service = Build(handler);

        var result = await service.GetNearbyAsync(41.0000, 29.0000, 1200);

        // Two schools (school + kindergarten), one health, one transit, one market; office ignored.
        result.Single(p => p.Category == "school").Count.ShouldBe(2);
        result.Single(p => p.Category == "health").Count.ShouldBe(1);
        result.Single(p => p.Category == "transit").Count.ShouldBe(1);
        result.Single(p => p.Category == "market").Count.ShouldBe(1);
        result.ShouldNotContain(p => p.Category == "office");
    }

    [Fact]
    public async Task GetNearbyAsync_ReportsNearestDistance_ForTheClosestInCategory()
    {
        var handler = new StubHandler(SampleBody);
        var service = Build(handler);

        var result = await service.GetNearbyAsync(41.0000, 29.0000, 1200);

        // The 41.0009 school is ~100 m away; the 41.0100 one is ~1.1 km. Nearest wins, and it's
        // a small positive distance rather than zero.
        var school = result.Single(p => p.Category == "school");
        school.NearestMeters.ShouldNotBeNull();
        school.NearestMeters!.Value.ShouldBeInRange(50, 200);
    }

    [Fact]
    public async Task GetNearbyAsync_ReturnsEmpty_OnHttpError()
    {
        var handler = new StubHandler("", HttpStatusCode.TooManyRequests);
        var service = Build(handler);

        var result = await service.GetNearbyAsync(41.0000, 29.0000, 1200);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetNearbyAsync_CachesByRoundedLocation()
    {
        var handler = new StubHandler(SampleBody);
        var service = Build(handler);

        await service.GetNearbyAsync(41.00001, 29.00001, 1200);
        await service.GetNearbyAsync(41.00004, 29.00002, 1200); // same 3-decimal cell

        handler.Calls.ShouldBe(1);
    }
}
