using FluentValidation.TestHelper;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Domain.Enums;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class CreateListingCommandValidatorTests
{
    private readonly CreateListingCommandValidator _validator = new();

    private static CreateListingCommand Valid() => new()
    {
        Title = "Nice flat",
        Description = "A description",
        Price = 100_000,
        Currency = "TRY",
        AreaSqMeters = 90,
        Bedrooms = 2,
        Bathrooms = 1,
        Address = "Kadıköy, İstanbul",
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment
    };

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyTitle_FailsValidation()
    {
        var command = Valid();
        command.Title = "";
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void ZeroPrice_FailsValidation()
    {
        var command = Valid();
        command.Price = 0;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Price);
    }

    // The address used to be required only by MVC's implicit check on non-nullable strings, which
    // has been turned off because its message could not be translated. The rule lives here now, so
    // this is what keeps a listing from being published with no address at all.
    [Fact]
    public void EmptyAddress_FailsValidation()
    {
        var command = Valid();
        command.Address = "";
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Address);
    }

    [Fact]
    public void ZeroArea_FailsValidation()
    {
        var command = Valid();
        command.AreaSqMeters = 0;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.AreaSqMeters);
    }
}