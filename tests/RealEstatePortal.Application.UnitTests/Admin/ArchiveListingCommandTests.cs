using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Admin.Commands.ArchiveListing;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Admin;

public class ArchiveListingCommandTests
{
    [Fact]
    public async Task Handle_SetsStatusToArchived_AndSaves()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1" };
        listing.Publish();   // Active

        var set = new List<Listing> { listing }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var handler = new ArchiveListingCommandHandler(ctx);

        await handler.Handle(new ArchiveListingCommand(1), CancellationToken.None);

        listing.Status.ShouldBe(ListingStatus.Archived);
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMissing_ThrowsNotFound()
    {
        var set = new List<Listing>().BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(set);

        var handler = new ArchiveListingCommandHandler(ctx);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new ArchiveListingCommand(9), CancellationToken.None));
    }
}