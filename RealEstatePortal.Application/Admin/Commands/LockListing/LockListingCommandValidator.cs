using FluentValidation;

namespace RealEstatePortal.Application.Admin.Commands.LockListing;

public class LockListingCommandValidator : AbstractValidator<LockListingCommand>
{
    public LockListingCommandValidator()
    {
        RuleFor(v => v.Reason)
            .NotEmpty().WithMessage("Please give a reason for locking this listing.")
            .MaximumLength(500);
    }
}