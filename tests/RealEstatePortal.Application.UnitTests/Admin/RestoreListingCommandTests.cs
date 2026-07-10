using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Admin.Commands.RestoreListing;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Admin;

public class RestoreListingCommandTests
{
    [Fact]
    public async Task Handle_ReturnsArchivedListingToDraft()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        listing.Publish();
        listing.Archive();   // Archived

        var set = new List<Listing> { listing }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var handler = new RestoreListingCommandHandler(ctx);

        await handler.Handle(new RestoreListingCommand(1), CancellationToken.None);

        listing.Status.ShouldBe(ListingStatus.Draft);
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}