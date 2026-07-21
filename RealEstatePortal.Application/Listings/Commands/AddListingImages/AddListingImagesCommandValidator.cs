using FluentValidation;

namespace RealEstatePortal.Application.Listings.Commands.AddListingImages;

// Shared limits so the validator and handler agree on the numbers.
public static class ListingImageRules
{
    public const int MaxImagesPerListing = 20;
    public const int MaxImageBytes = 8 * 1024 * 1024; // 8 MB
    public const int MaxImageMegabytes = MaxImageBytes / (1024 * 1024);
}

public class AddListingImagesCommandValidator : AbstractValidator<AddListingImagesCommand>
{
    public AddListingImagesCommandValidator()
    {
        RuleFor(v => v.Images)
            .NotEmpty().WithMessage("Please choose at least one image.");

        RuleFor(v => v.Images.Count)
            .LessThanOrEqualTo(ListingImageRules.MaxImagesPerListing)
            .WithMessage($"You can upload at most {ListingImageRules.MaxImagesPerListing} images at once.");

        RuleForEach(v => v.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.Content)
                .NotEmpty().WithMessage("One of the selected files is empty.")
                .Must(c => c == null || c.Length <= ListingImageRules.MaxImageBytes)
                .WithMessage($"Each image must be {ListingImageRules.MaxImageMegabytes} MB or smaller.");

            image.RuleFor(i => i.ContentType)
                .Must(ct => ct != null && ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only image files are allowed.");
        });
    }
}
