using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Domain.Enums;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class PublishListingIntegrationTests : IntegrationTestBase
{
    public PublishListingIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PublishingListing_DispatchesEvent_AndEmailsOwner()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = "Event flat",
            Description = "desc",
            Address = "Kadıköy, İstanbul",
            Price = 100_000,
            Currency = "TRY",
            AreaSqMeters = 90,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });

        await Fixture.SendAsync(new PublishListingCommand(id));

        // The domain event was dispatched through the real interceptor + MediatR,
        // reaching the handler, which emailed the owner.
        await Fixture.EmailService.Received(1).SendAsync(
            "owner@test.local",
            Arg.Is<string>(s => s.Contains("live")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}