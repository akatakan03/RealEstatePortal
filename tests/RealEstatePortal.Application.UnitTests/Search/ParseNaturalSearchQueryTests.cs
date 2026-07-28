using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Queries.ParseNaturalSearch;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Search;

public class ParseNaturalSearchQueryTests
{
    private readonly INaturalLanguageSearchParser _parser = Substitute.For<INaturalLanguageSearchParser>();
    private readonly IGeocodingService _geocoding = Substitute.For<IGeocodingService>();

    private ParseNaturalSearchQueryHandler Handler() => new(_parser, _geocoding);

    [Fact]
    public async Task Handle_FallsBackToKeyword_WhenParserUnavailable()
    {
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ParsedSearchFilter?)null);

        var result = await Handler().Handle(new ParseNaturalSearchQuery("  Kadıköy daire  "), CancellationToken.None);

        result.AiApplied.ShouldBeFalse();
        result.Filter.Keyword.ShouldBe("Kadıköy daire");
        result.UnmatchedCriteria.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_MapsFilterAndGeocodesLocationToCentre()
    {
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ParsedSearchFilter
        {
            ListingType = ListingType.Sale,
            MinBedrooms = 2,
            MaxPrice = 6_000_000,
            Balcony = true,
            LocationText = "Ataşehir",
            UnmatchedCriteria = new[] { "deniz manzaralı" }
        });
        _geocoding.GeocodeAsync("Ataşehir", Arg.Any<CancellationToken>())
            .Returns(new GeoCoordinate(40.99, 29.12));

        var result = await Handler().Handle(new ParseNaturalSearchQuery("Ataşehir'de deniz manzaralı 2+1 satılık"), CancellationToken.None);

        result.AiApplied.ShouldBeTrue();
        result.Filter.ListingType.ShouldBe(ListingType.Sale);
        result.Filter.MinBedrooms.ShouldBe(2);
        result.Filter.MaxPrice.ShouldBe(6_000_000);
        result.Filter.Balcony.ShouldBe(true);
        result.Filter.CenterLat.ShouldBe(40.99);
        result.Filter.CenterLng.ShouldBe(29.12);
        result.Filter.RadiusKm.ShouldNotBeNull();
        result.UnmatchedCriteria.ShouldContain("deniz manzaralı");
    }

    [Fact]
    public async Task Handle_KeepsLocationAsKeyword_WhenGeocodeFails()
    {
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ParsedSearchFilter
        {
            LocationText = "Bilinmeyen Mahalle"
        });
        _geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        var result = await Handler().Handle(new ParseNaturalSearchQuery("Bilinmeyen Mahalle'de ev"), CancellationToken.None);

        result.Filter.CenterLat.ShouldBeNull();
        result.Filter.Keyword.ShouldBe("Bilinmeyen Mahalle");
    }
}
