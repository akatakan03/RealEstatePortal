using FluentValidation;

namespace RealEstatePortal.Application.Listings.Commands.UpdateListing;

public class UpdateListingCommandValidator : AbstractValidator<UpdateListingCommand>
{
    private static bool BeValidLatitude(double? lat) => lat is null || (lat >= -90 && lat <= 90);
    private static bool BeValidLongitude(double? lng) => lng is null || (lng >= -180 && lng <= 180);
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
        RuleFor(v => v.Latitude).Must(BeValidLatitude).WithMessage("Latitude is out of range.");
        RuleFor(v => v.Longitude).Must(BeValidLongitude).WithMessage("Longitude is out of range.");
        RuleFor(v => v.FloorNumber)
            .GreaterThanOrEqualTo(-5).When(v => v.FloorNumber.HasValue)
            .WithMessage("Floor number looks invalid.");

        RuleFor(v => v.TotalFloors)
            .GreaterThan(0).When(v => v.TotalFloors.HasValue)
            .WithMessage("Total floors must be positive.");

        RuleFor(v => v.FloorNumber)
            .LessThanOrEqualTo(v => v.TotalFloors!.Value)
            .When(v => v.FloorNumber.HasValue && v.TotalFloors.HasValue)
            .WithMessage("Floor can't be higher than the building's total floors.");

        RuleFor(v => v.BuildingAge)
            .InclusiveBetween(0, 200).When(v => v.BuildingAge.HasValue)
            .WithMessage("Building age looks invalid.");

        RuleFor(v => v.MonthlyDues)
            .GreaterThanOrEqualTo(0).When(v => v.MonthlyDues.HasValue)
            .WithMessage("Monthly dues can't be negative.");
    }
}