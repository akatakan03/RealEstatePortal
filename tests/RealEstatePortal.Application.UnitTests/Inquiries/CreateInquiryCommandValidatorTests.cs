using FluentValidation.TestHelper;
using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Inquiries;

public class CreateInquiryCommandValidatorTests
{
    private readonly CreateInquiryCommandValidator _validator = new();

    private static CreateInquiryCommand Valid() => new()
    {
        ListingId = 1,
        Name = "Buyer",
        Email = "buyer@test.local",
        Phone = "05551112233",
        Message = "I'm interested."
    };

    [Fact]
    public void ValidCommand_Passes()
        => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyMessage_Fails()
    {
        var c = Valid(); c.Message = "";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        var c = Valid(); c.Email = "not-an-email";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void MissingListingId_Fails()
    {
        var c = Valid(); c.ListingId = 0;
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.ListingId);
    }
}