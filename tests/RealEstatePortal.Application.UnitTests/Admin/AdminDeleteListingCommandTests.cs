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
    public async Task Handle_MarksTheListingDeleted_WithoutRemovingTheRow()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        listing.Media.Add(new ListingMedia { ObjectKey = "k1", ThumbnailKey = "t1" });

        var set = new List<Listing> { listing }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var user = Substitute.For<IUser>();
        user.Id.Returns("admin-1");

        var handler = new AdminDeleteListingCommandHandler(ctx, TimeProvider.System, user);

        await handler.Handle(new AdminDeleteListingCommand(1), CancellationToken.None);

        listing.IsDeleted.ShouldBeTrue();
        listing.DeletedBy.ShouldBe("admin-1");

        // The photos stay in storage: they are what makes a restore worth having, and the
        // purge sweep is what eventually removes them.
        ctx.Listings.DidNotReceive().Remove(Arg.Any<Listing>());
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMissing_ThrowsNotFound()
    {
        var set = new List<Listing>().BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var handler = new AdminDeleteListingCommandHandler(
            ctx, TimeProvider.System, Substitute.For<IUser>());

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new AdminDeleteListingCommand(9), CancellationToken.None));
    }
}
