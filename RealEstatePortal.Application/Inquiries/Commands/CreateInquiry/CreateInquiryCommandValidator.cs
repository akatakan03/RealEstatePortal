using FluentValidation;

namespace RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;

public class CreateInquiryCommandValidator : AbstractValidator<CreateInquiryCommand>
{
    public CreateInquiryCommandValidator()
    {
        RuleFor(v => v.ListingId).GreaterThan(0);
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(v => v.Phone).MaximumLength(40);
        RuleFor(v => v.Message).NotEmpty().MaximumLength(2000)
            .WithMessage("Please enter a message.");
    }
}