using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Commands.AddListingImages;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class AddListingImagesCommandTests
{
    private static (IApplicationDbContext ctx, IImageProcessor img, IFileStorageService storage, IUser user)
        Deps(List<Listing> listings, string currentUser = "agent-1")
    {
        var set = listings.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var img = Substitute.For<IImageProcessor>();
        img.ProcessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessedImage(new byte[] { 1 }, new byte[] { 2 }));  // display, thumb

        var storage = Substitute.For<IFileStorageService>();

        var user = Substitute.For<IUser>();
        user.Id.Returns(currentUser);
        return (ctx, img, storage, user);
    }

    private static AddListingImagesCommand Command(int listingId, int imageCount)
    {
        var images = Enumerable.Range(0, imageCount)
            .Select(i => new ImageUploadDto(new byte[] { (byte)i }, $"photo{i}.jpg", "image/jpeg"))
            .ToList();
        return new AddListingImagesCommand(listingId, images);
    }

    [Fact]
    public async Task Handle_ProcessesAndUploadsEachImage_AndAddsMediaRows()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        var (ctx, img, storage, user) = Deps(new List<Listing> { listing });
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await handler.Handle(Command(1, imageCount: 2), CancellationToken.None);

        // Each image is processed once...
        await img.Received(2).ProcessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        // ...and both sizes (display + thumb) are uploaded for each -> 4 uploads.
        await storage.Received(4).UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Two media rows now hang off the listing.
        listing.Media.Count.ShouldBe(2);
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstEverPhoto_BecomesCover()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };  // no photos yet
        var (ctx, img, storage, user) = Deps(new List<Listing> { listing });
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await handler.Handle(Command(1, imageCount: 2), CancellationToken.None);

        // Exactly one cover, and it's the first added.
        listing.Media.Count(m => m.IsCover).ShouldBe(1);
        listing.Media.First().IsCover.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenListingAlreadyHasCover_DoesNotAddSecondCover()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        listing.Media.Add(new ListingMedia { ObjectKey = "existing", ThumbnailKey = "existing-t", IsCover = true });

        var (ctx, img, storage, user) = Deps(new List<Listing> { listing });
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await handler.Handle(Command(1, imageCount: 1), CancellationToken.None);

        listing.Media.Count(m => m.IsCover).ShouldBe(1);   // still exactly one
    }

    [Fact]
    public async Task Handle_WhenTotalWouldExceedMax_ThrowsValidation_AndUploadsNothing()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        for (var i = 0; i < ListingImageRules.MaxImagesPerListing; i++)
            listing.Media.Add(new ListingMedia { ObjectKey = $"k{i}", ThumbnailKey = $"t{i}" });

        var (ctx, img, storage, user) = Deps(new List<Listing> { listing });
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await Should.ThrowAsync<ValidationException>(
            () => handler.Handle(Command(1, imageCount: 1), CancellationToken.None));

        await img.DidNotReceive().ProcessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await storage.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenImageCannotBeProcessed_ThrowsValidation()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        var (ctx, img, storage, user) = Deps(new List<Listing> { listing });
        img.ProcessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("corrupt / not an image"));
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await Should.ThrowAsync<ValidationException>(
            () => handler.Handle(Command(1, imageCount: 1), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ThrowsForbidden_AndUploadsNothing()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        var (ctx, img, storage, user) = Deps(new List<Listing> { listing }, currentUser: "agent-2");
        var handler = new AddListingImagesCommandHandler(ctx, img, storage, user);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(Command(1, imageCount: 1), CancellationToken.None));

        // Ownership is checked BEFORE any processing/upload happens.
        await img.DidNotReceive().ProcessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await storage.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}