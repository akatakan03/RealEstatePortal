using Shouldly;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Domain.UnitTests.ValueObjects;

public class GeoLocationTests
{
    [Fact]
    public void Constructor_WithValidCoordinates_SetsProperties()
    {
        var location = new GeoLocation(41.0082, 28.9784);

        location.Latitude.ShouldBe(41.0082);
        location.Longitude.ShouldBe(28.9784);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Constructor_WithLatitudeOutOfRange_Throws(double latitude)
    {
        Should.Throw<ArgumentException>(() => new GeoLocation(latitude, 0));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Constructor_WithLongitudeOutOfRange_Throws(double longitude)
    {
        Should.Throw<ArgumentException>(() => new GeoLocation(0, longitude));
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(90)]
    public void Constructor_AtLatitudeBoundaries_IsValid(double latitude)
    {
        new GeoLocation(latitude, 0).Latitude.ShouldBe(latitude);
    }
}