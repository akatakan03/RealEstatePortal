using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.ValueObjects;

public class GeoLocation : ValueObject
{
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    private GeoLocation() { } // for EF Core

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentException("Latitude must be between -90 and 90.", nameof(latitude));
        if (longitude is < -180 or > 180)
            throw new ArgumentException("Longitude must be between -180 and 180.", nameof(longitude));

        Latitude = latitude;
        Longitude = longitude;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}