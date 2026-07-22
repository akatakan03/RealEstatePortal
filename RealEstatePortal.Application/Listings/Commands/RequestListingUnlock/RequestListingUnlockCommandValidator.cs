using FluentValidation;

namespace RealEstatePortal.Application.Listings.Commands.RequestListingUnlock;

public class RequestListingUnlockCommandValidator : AbstractValidator<RequestListingUnlockCommand>
{
    public RequestListingUnlockCommandValidator()
    {
        RuleFor(v => v.Note)
            .MaximumLength(1000)
            .WithMessage("Your note is too long (1000 characters max).");
    }
}
