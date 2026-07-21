using System.Collections.Generic;
using System.Linq;
using RealEstatePortal.Application.Listings.Commands.AddListingImages;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class AddListingImagesCommandValidatorTests
{
    private readonly AddListingImagesCommandValidator _validator = new();

    private static AddListingImagesCommand Command(params ImageUploadDto[] images) =>
        new(1, images);

    private static ImageUploadDto Img(int bytes, string contentType = "image/jpeg") =>
        new(new byte[bytes], "photo.jpg", contentType);

    [Fact]
    public void ValidUpload_Passes()
    {
        var result = _validator.Validate(Command(Img(1024), Img(2048)));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyImageList_Fails()
    {
        var result = _validator.Validate(Command());
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void OversizeImage_Fails()
    {
        var result = _validator.Validate(Command(Img(ListingImageRules.MaxImageBytes + 1)));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void NonImageContentType_Fails()
    {
        var result = _validator.Validate(Command(Img(1024, contentType: "application/pdf")));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TooManyImagesAtOnce_Fails()
    {
        var many = Enumerable.Range(0, ListingImageRules.MaxImagesPerListing + 1)
            .Select(_ => Img(64))
            .ToArray();
        var result = _validator.Validate(Command(many));
        result.IsValid.ShouldBeFalse();
    }
}
