using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Admin;

public class AdminDeleteListingCommandTests
{
    [Fact]
    public async Task Handle_DeletesEveryPhotoFromStorage_ThenRemovesListing()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        listing.Media.Add(new ListingMedia { ObjectKey = "k1", ThumbnailKey = "t1" });
        listing.Media.Add(new ListingMedia { ObjectKey = "k2", ThumbnailKey = "t2" });

        var set = new List<Listing> { listing }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var storage = Substitute.For<IFileStorageService>();
        var handler = new AdminDeleteListingCommandHandler(ctx, storage);

        await handler.Handle(new AdminDeleteListingCommand(1), CancellationToken.None);

        // Both objects for both photos are cleaned from R2 — no orphans.
        await storage.Received(1).DeleteAsync("k1", Arg.Any<CancellationToken>());
        await storage.Received(1).DeleteAsync("t1", Arg.Any<CancellationToken>());
        await storage.Received(1).DeleteAsync("k2", Arg.Any<CancellationToken>());
        await storage.Received(1).DeleteAsync("t2", Arg.Any<CancellationToken>());

        ctx.Listings.Received(1).Remove(listing);
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMissing_ThrowsNotFound()
    {
        var set = new List<Listing>().BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var handler = new AdminDeleteListingCommandHandler(ctx, Substitute.For<IFileStorageService>());

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new AdminDeleteListingCommand(9), CancellationToken.None));
    }
}