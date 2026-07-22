using System.Linq;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingPriceHistoryIntegrationTests : IntegrationTestBase
{
    public ListingPriceHistoryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreatingThenRepricing_BuildsTheTimeline_ExposedOnTheDetail()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = "Sea view flat",
            Description = "A bright apartment",
            Address = "Kadıköy, İstanbul",
            Price = 100_000,
            Currency = "TRY",
            AreaSqMeters = 90,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });

        await Fixture.SendAsync(new PublishListingCommand(id));   // detail query only returns Active

        // Agent drops the price.
        await Fixture.SendAsync(new UpdateListingCommand
        {
            Id = id,
            Title = "Sea view flat",
            Description = "A bright apartment",
            Address = "Kadıköy, İstanbul",
            Price = 90_000,
            Currency = "TRY",
            AreaSqMeters = 90,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });

        var detail = await Fixture.SendAsync(new GetListingDetailQuery(id));

        detail.ShouldNotBeNull();
        detail!.PriceHistory.Count.ShouldBe(2);                 // initial + the reprice
        detail.PriceHistory[0].Amount.ShouldBe(100_000);        // oldest first
        detail.PriceHistory[1].Amount.ShouldBe(90_000);
        detail.PriceAmount.ShouldBe(90_000);                    // current price is the latest
    }

    [Fact]
    public async Task UpdatingWithoutChangingThePrice_AddsNoExtraPoint()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = "Garden house",
            Description = "Quiet street",
            Address = "Beşiktaş, İstanbul",
            Price = 250_000,
            Currency = "TRY",
            AreaSqMeters = 120,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });

        await Fixture.SendAsync(new PublishListingCommand(id));

        // Same price, only the title edited.
        await Fixture.SendAsync(new UpdateListingCommand
        {
            Id = id,
            Title = "Garden house (renovated)",
            Description = "Quiet street",
            Address = "Beşiktaş, İstanbul",
            Price = 250_000,
            Currency = "TRY",
            AreaSqMeters = 120,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });

        var detail = await Fixture.SendAsync(new GetListingDetailQuery(id));

        detail!.PriceHistory.Count.ShouldBe(1);   // only the initial point
    }
}
