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

    [Fact]
    public void ZeroArea_FailsValidation()
    {
        var command = Valid();
        command.AreaSqMeters = 0;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.AreaSqMeters);
    }
}