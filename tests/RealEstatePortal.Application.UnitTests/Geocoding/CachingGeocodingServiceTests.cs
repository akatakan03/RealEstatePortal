using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Infrastructure.Geocoding;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Geocoding;

public class CachingGeocodingServiceTests
{
    private static CachingGeocodingService Build(IGeocodingService inner) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task RepeatedAddress_IsServedFromCache_WithoutCallingInnerAgain()
    {
        var inner = Substitute.For<IGeocodingService>();
        inner.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GeoCoordinate(41.0082, 28.9784));
        var service = Build(inner);

        var first = await service.GeocodeAsync("Kadıköy, İstanbul");
        // Same place, different casing/whitespace — should still hit the cache.
        var second = await service.GeocodeAsync("  kadıköy, İstanbul  ");

        first.ShouldBe(second);
        await inner.Received(1).GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedLookup_IsNotCached_SoInnerIsRetried()
    {
        var inner = Substitute.For<IGeocodingService>();
        inner.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        var service = Build(inner);

        await service.GeocodeAsync("nowhere in particular");
        await service.GeocodeAsync("nowhere in particular");

        await inner.Received(2).GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
