using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class UpdateListingCommandTests
{
    private static UpdateListingCommand EditCommand(int id) => new()
    {
        Id = id,
        Title = "Updated",
        Description = "desc",
        Address = "same address",
        Price = 120_000,
        Currency = "TRY",
        AreaSqMeters = 90,
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment
    };

    private static (IApplicationDbContext ctx, IUser user, IGeocodingService geo) Deps(
        List<Listing> listings, string currentUserId)
    {
        // Build the mock DbSet first, then assign it — not inline inside Returns().
        var listingsDbSet = listings.BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.Listings.Returns(listingsDbSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns(currentUserId);

        var geo = Substitute.For<IGeocodingService>();
        return (context, user, geo);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ThrowsForbiddenAccess()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1", Address = "same address" };
        var (ctx, user, geo) = Deps(new List<Listing> { listing }, currentUserId: "agent-2");
        var handler = new UpdateListingCommandHandler(ctx, user, geo);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(EditCommand(1), CancellationToken.None));

        await ctx.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingMissing_ThrowsNotFound()
    {
        var (ctx, user, geo) = Deps(new List<Listing>(), currentUserId: "agent-1");
        var handler = new UpdateListingCommandHandler(ctx, user, geo);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(EditCommand(99), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenOwner_UpdatesAndSaves()
    {
        var listing = new Listing { Id = 1, OwnerId = "agent-1", Title = "Old", Address = "same address" };
        var (ctx, user, geo) = Deps(new List<Listing> { listing }, currentUserId: "agent-1");
        var handler = new UpdateListingCommandHandler(ctx, user, geo);

        await handler.Handle(EditCommand(1), CancellationToken.None);

        listing.Title.ShouldBe("Updated");
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}