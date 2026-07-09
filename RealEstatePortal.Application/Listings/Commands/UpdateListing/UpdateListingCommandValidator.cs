using FluentValidation;

namespace RealEstatePortal.Application.Listings.Commands.UpdateListing;

public class UpdateListingCommandValidator : AbstractValidator<UpdateListingCommand>
{
    public UpdateListingCommandValidator()
    {
        RuleFor(v => v.Id).GreaterThan(0);
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).NotEmpty().MaximumLength(4000);
        RuleFor(v => v.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
        RuleFor(v => v.Currency).NotEmpty().MaximumLength(3);
        RuleFor(v => v.AreaSqMeters).GreaterThan(0).WithMessage("Area must be greater than zero.");
        RuleFor(v => v.Bedrooms).GreaterThanOrEqualTo(0);
        RuleFor(v => v.Bathrooms).GreaterThanOrEqualTo(0);
        RuleFor(v => v.ListingType).IsInEnum();
        RuleFor(v => v.PropertyType).IsInEnum();
    }
}