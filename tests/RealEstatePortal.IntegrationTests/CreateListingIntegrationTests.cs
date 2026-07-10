using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class CreateListingIntegrationTests : IntegrationTestBase
{
    public CreateListingIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    private static CreateListingCommand ValidCommand() => new()
    {
        Title = "Integration flat",
        Description = "A real description",
        Address = "Kadıköy, İstanbul",
        Price = 100_000,
        Currency = "TRY",
        AreaSqMeters = 90,
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment
    };

    [Fact]
    public async Task InvalidCommand_IsRejectedByThePipeline()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var command = ValidCommand();
        command.Title = "";   // invalid

        // Proves the ValidationBehaviour actually runs in the real MediatR pipeline.
        await Should.ThrowAsync<ValidationException>(() => Fixture.SendAsync(command));
    }

    [Fact]
    public async Task ValidCommand_PersistsToRealDatabase()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.SendAsync(ValidCommand());

        var saved = await Fixture.ExecuteDbAsync(db =>
            db.Listings.FirstOrDefaultAsync(l => l.Id == id));

        saved.ShouldNotBeNull();
        saved!.OwnerId.ShouldBe("agent-1");
        saved.CreatedBy.ShouldBe("agent-1");        // audit interceptor ran
        saved.Price.Amount.ShouldBe(100_000);       // owned value object persisted + read back
        saved.Price.Currency.ShouldBe("TRY");
        saved.Status.ShouldBe(ListingStatus.Draft);
    }
}