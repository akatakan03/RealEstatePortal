using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Favorites.Commands.ToggleFavorite;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Favorites;

public class ToggleFavoriteCommandTests
{
    private static (ToggleFavoriteCommandHandler handler, IApplicationDbContext ctx) Build(
        List<Listing> listings, List<Favorite> favorites, string userId = "member-1")
    {
        var listingsSet = listings.BuildMockDbSet();
        var favoritesSet = favorites.BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);
        ctx.Favorites.Returns(favoritesSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns(userId);

        return (new ToggleFavoriteCommandHandler(ctx, user), ctx);
    }

    [Fact]
    public async Task Toggle_WhenNotYetFavorited_AddsAndReturnsTrue()
    {
        var listing = new Listing { Id = 1 };
        var (handler, ctx) = Build(new List<Listing> { listing }, new List<Favorite>());

        var result = await handler.Handle(new ToggleFavoriteCommand(1), CancellationToken.None);

        result.ShouldBeTrue();   // now favorited
        ctx.Favorites.Received(1).Add(Arg.Is<Favorite>(f => f.ListingId == 1 && f.UserId == "member-1"));
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_WhenAlreadyFavorited_RemovesAndReturnsFalse()
    {
        var listing = new Listing { Id = 1 };
        var existing = new Favorite { Id = 5, ListingId = 1, UserId = "member-1" };
        var (handler, ctx) = Build(new List<Listing> { listing }, new List<Favorite> { existing });

        var result = await handler.Handle(new ToggleFavoriteCommand(1), CancellationToken.None);

        result.ShouldBeFalse();   // now un-favorited
        ctx.Favorites.Received(1).Remove(Arg.Is<Favorite>(f => f.Id == 5));
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_OnlyRemovesTheCurrentUsersFavorite()
    {
        // Same listing favorited by someone else — must not be touched.
        var listing = new Listing { Id = 1 };
        var mine = new Favorite { Id = 5, ListingId = 1, UserId = "member-1" };
        var someoneElses = new Favorite { Id = 6, ListingId = 1, UserId = "member-2" };
        var (handler, ctx) = Build(new List<Listing> { listing }, new List<Favorite> { mine, someoneElses });

        await handler.Handle(new ToggleFavoriteCommand(1), CancellationToken.None);

        ctx.Favorites.Received(1).Remove(Arg.Is<Favorite>(f => f.Id == 5));       // mine
        ctx.Favorites.DidNotReceive().Remove(Arg.Is<Favorite>(f => f.Id == 6));   // not theirs
    }

    [Fact]
    public async Task Toggle_WhenListingDoesNotExist_ThrowsNotFound()
    {
        var (handler, _) = Build(new List<Listing>(), new List<Favorite>());

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new ToggleFavoriteCommand(99), CancellationToken.None));
    }
}