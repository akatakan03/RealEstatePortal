using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RealEstatePortal.Infrastructure.Geocoding;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Geocoding;

public class NominatimGeocodingServiceTests
{
    private static NominatimGeocodingService Build(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new System.Uri("https://nominatim.test/") };
        return new NominatimGeocodingService(client, Substitute.For<ILogger<NominatimGeocodingService>>());
    }

    [Fact]
    public async Task Geocode_WhenFirstQueryResolves_ReturnsCoordinates_WithNoFallback()
    {
        var handler = new StubHttpMessageHandler(
            "[{\"lat\":\"41.0082\",\"lon\":\"28.9784\"}]");
        var service = Build(handler);

        var result = await service.GeocodeAsync("Kadıköy, İstanbul", CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Latitude.ShouldBe(41.0082);
        result.Longitude.ShouldBe(28.9784);
        handler.RequestedUris.Count.ShouldBe(1);   // resolved on the first try, no fallback needed
    }

    [Fact]
    public async Task Geocode_WhenFullAddressEmpty_FallsBackToSimplerQuery()
    {
        // First response empty (full address), second returns a hit (simplified).
        var handler = new StubHttpMessageHandler(
            "[]",
            "[{\"lat\":\"40.76\",\"lon\":\"29.94\"}]");
        var service = Build(handler);

        var result = await service.GeocodeAsync(
            "Fatih Mh., Kartepe Cd., Kartepe, Kocaeli", CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Latitude.ShouldBe(40.76);
        handler.RequestedUris.Count.ShouldBeGreaterThan(1);   // proves the fallback fired
    }

    [Fact]
    public async Task Geocode_WhenNothingResolves_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler("[]", "[]", "[]", "[]");
        var service = Build(handler);

        var result = await service.GeocodeAsync("nowhere at all xyz", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Geocode_WithBlankInput_ReturnsNull_WithoutCallingHttp()
    {
        var handler = new StubHttpMessageHandler();
        var service = Build(handler);

        var result = await service.GeocodeAsync("   ", CancellationToken.None);

        result.ShouldBeNull();
        handler.RequestedUris.ShouldBeEmpty();   // short-circuits before any request
    }
}