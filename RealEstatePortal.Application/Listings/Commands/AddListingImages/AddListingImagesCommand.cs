using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Entities;
using ValidationException = RealEstatePortal.Application.Common.Exceptions.ValidationException;

namespace RealEstatePortal.Application.Listings.Commands.AddListingImages;

public record AddListingImagesCommand(int ListingId, IReadOnlyList<ImageUploadDto> Images) : IRequest;

public class AddListingImagesCommandHandler : IRequestHandler<AddListingImagesCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IImageProcessor _imageProcessor;
    private readonly IFileStorageService _storage;
    private readonly IUser _user;

    public AddListingImagesCommandHandler(
        IApplicationDbContext context,
        IImageProcessor imageProcessor,
        IFileStorageService storage,
        IUser user)
    {
        _context = context;
        _imageProcessor = imageProcessor;
        _storage = storage;
        _user = user;
    }

    public async Task Handle(AddListingImagesCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.GetOwnedListingAsync(request.ListingId, _user.Id, cancellationToken, includeMedia: true);

        // Total-per-listing cap (the validator only bounds a single upload).
        if (listing.Media.Count + request.Images.Count > ListingImageRules.MaxImagesPerListing)
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Images),
                    $"A listing can have at most {ListingImageRules.MaxImagesPerListing} images " +
                    $"(this one already has {listing.Media.Count}).")
            });

        var hasCover = listing.Media.Any(m => m.IsCover);
        var nextOrder = listing.Media.Count == 0 ? 0 : listing.Media.Max(m => m.Order) + 1;

        foreach (var img in request.Images)
        {
            ProcessedImage processed;
            try
            {
                processed = await _imageProcessor.ProcessAsync(img.Content, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A spoofed or corrupt file reaches here — surface a clean 400, not a 500.
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Images),
                        $"\"{img.FileName}\" could not be read as an image.")
                });
            }

            var id = Guid.NewGuid().ToString("N");
            var displayKey = $"listings/{listing.Id}/{id}.webp";
            var thumbKey = $"listings/{listing.Id}/{id}-thumb.webp";

            await _storage.UploadAsync(new MemoryStream(processed.Display), displayKey, "image/webp", cancellationToken);
            await _storage.UploadAsync(new MemoryStream(processed.Thumbnail), thumbKey, "image/webp", cancellationToken);

            listing.Media.Add(new ListingMedia
            {
                ObjectKey = displayKey,
                ThumbnailKey = thumbKey,
                Order = nextOrder++,
                IsCover = !hasCover     // the very first photo becomes the cover
            });

            hasCover = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}