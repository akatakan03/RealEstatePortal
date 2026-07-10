using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class CreateInquiryIntegrationTests : IntegrationTestBase
{
    public CreateInquiryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Inquiry_IsPersisted_AndAgentIsEmailed()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var listingId = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = "Inquiry flat",
            Description = "desc",
            Address = "Kadıköy, İstanbul",
            Price = 100_000,
            Currency = "TRY",
            AreaSqMeters = 90,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment
        });
        await Fixture.SendAsync(new PublishListingCommand(listingId));

        // Publishing already sent the "listing is live" email; clear so we isolate the inquiry email.
        Fixture.EmailService.ClearReceivedCalls();

        await Fixture.SendAsync(new CreateInquiryCommand
        {
            ListingId = listingId,
            Name = "Buyer",
            Email = "buyer@test.local",
            Message = "Is this still available?"
        });

        var count = await Fixture.ExecuteDbAsync(db => db.Inquiries.CountAsync());
        count.ShouldBe(1);

        await Fixture.EmailService.Received(1).SendAsync(
            "owner@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}