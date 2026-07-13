using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

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

        var hasCover = listing.Media.Any(m => m.IsCover);
        var nextOrder = listing.Media.Count == 0 ? 0 : listing.Media.Max(m => m.Order) + 1;

        foreach (var img in request.Images)
        {
            var processed = await _imageProcessor.ProcessAsync(img.Content, cancellationToken);

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