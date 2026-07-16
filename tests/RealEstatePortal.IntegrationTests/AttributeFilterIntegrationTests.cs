using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class AttributeFilterIntegrationTests : IntegrationTestBase
{
    public AttributeFilterIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    // Creates + publishes a listing with the given attributes, returns nothing (we query by filter).
    private async Task SeedAsync(
        string title, bool furnished, bool parking, HeatingType heating, decimal? dues)
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = title,
            Description = "desc",
            Address = "Kadıköy, İstanbul",
            Price = 100_000,
            Currency = "TRY",
            AreaSqMeters = 90,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            IsFurnished = furnished,
            HasParking = parking,
            Heating = heating,
            MonthlyDues = dues
        });
        await Fixture.SendAsync(new PublishListingCommand(id));
    }

    [Fact]
    public async Task Furnished_Filter_ReturnsOnlyFurnished()
    {
        await SeedAsync("Furnished flat", furnished: true, parking: false, HeatingType.NaturalGas, 500);
        await SeedAsync("Empty flat", furnished: false, parking: false, HeatingType.NaturalGas, 500);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery { Furnished = true });

        result.Items.ShouldContain(x => x.Title == "Furnished flat");
        result.Items.ShouldNotContain(x => x.Title == "Empty flat");
    }

    [Fact]
    public async Task Parking_Filter_ReturnsOnlyWithParking()
    {
        await SeedAsync("Has parking", furnished: false, parking: true, HeatingType.NaturalGas, 500);
        await SeedAsync("No parking", furnished: false, parking: false, HeatingType.NaturalGas, 500);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery { Parking = true });

        result.Items.ShouldContain(x => x.Title == "Has parking");
        result.Items.ShouldNotContain(x => x.Title == "No parking");
    }

    [Fact]
    public async Task Heating_Filter_MatchesExactType()
    {
        await SeedAsync("Gas heated", furnished: false, parking: false, HeatingType.NaturalGas, 500);
        await SeedAsync("Central heated", furnished: false, parking: false, HeatingType.CentralHeating, 500);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery { Heating = HeatingType.NaturalGas });

        result.Items.ShouldContain(x => x.Title == "Gas heated");
        result.Items.ShouldNotContain(x => x.Title == "Central heated");
    }

    [Fact]
    public async Task MaxDues_Filter_ExcludesAboveCap_AndNullDues()
    {
        await SeedAsync("Cheap dues", furnished: false, parking: false, HeatingType.NaturalGas, dues: 300);
        await SeedAsync("Pricey dues", furnished: false, parking: false, HeatingType.NaturalGas, dues: 2000);
        await SeedAsync("No dues stated", furnished: false, parking: false, HeatingType.NaturalGas, dues: null);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery { MaxDues = 1000 });

        result.Items.ShouldContain(x => x.Title == "Cheap dues");
        result.Items.ShouldNotContain(x => x.Title == "Pricey dues");
        result.Items.ShouldNotContain(x => x.Title == "No dues stated");   // unknown dues excluded when capping
    }

    [Fact]
    public async Task NoFilter_ReturnsEverything()
    {
        await SeedAsync("A", furnished: true, parking: true, HeatingType.NaturalGas, 500);
        await SeedAsync("B", furnished: false, parking: false, HeatingType.Stove, null);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery());

        result.Items.Count.ShouldBe(2);   // no filters -> all active listings
    }

    [Fact]
    public async Task CombinedFilters_AllApply()
    {
        await SeedAsync("Furnished + parking", furnished: true, parking: true, HeatingType.NaturalGas, 400);
        await SeedAsync("Furnished only", furnished: true, parking: false, HeatingType.NaturalGas, 400);

        var result = await Fixture.SendAsync(new GetPublicListingsQuery { Furnished = true, Parking = true });

        result.Items.ShouldContain(x => x.Title == "Furnished + parking");
        result.Items.ShouldNotContain(x => x.Title == "Furnished only");
    }
}