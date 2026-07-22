using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Listings.Commands.RequestListingUnlock;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class RequestListingUnlockIntegrationTests : IntegrationTestBase
{
    public RequestListingUnlockIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Agent_CanRequestReReview_OnTheirLockedListing()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed("agent-1");
            listing.Lock("The photos don't match");
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Fixture.SendAsync(new RequestListingUnlockCommand(id, "  Replaced the photos  "));

        var saved = await Fixture.ExecuteDbAsync(db =>
            db.Listings.FirstAsync(l => l.Id == id));

        saved.UnlockRequested.ShouldBeTrue();
        saved.UnlockRequestNote.ShouldBe("Replaced the photos");
        saved.UnlockRequestedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task PendingUnlockRequestsQuery_ReturnsOnlyListingsAwaitingReview()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var (requestedId, _) = await Fixture.ExecuteDbAsync(async db =>
        {
            var appealed = Seed("agent-1");
            appealed.Lock("Bad photos");
            appealed.RequestUnlock("Fixed them", System.DateTimeOffset.UtcNow);

            var lockedOnly = Seed("agent-1");     // locked but no request -> excluded
            lockedOnly.Slug = "locked-only";
            lockedOnly.Lock("Wrong address");

            db.Listings.AddRange(appealed, lockedOnly);
            await db.SaveChangesAsync(CancellationToken.None);
            return (appealed.Id, lockedOnly.Id);
        });

        var pending = await Fixture.SendAsync(
            new RealEstatePortal.Application.Admin.Queries.GetListingsForModeration.GetPendingUnlockRequestsQuery());

        pending.Select(p => p.Id).ShouldBe(new[] { requestedId });
        pending.Single().UnlockRequestNote.ShouldBe("Fixed them");
    }

    [Fact]
    public async Task RequestingOnAnUnlockedListing_IsRejected()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed("agent-1");   // active, not locked
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => Fixture.SendAsync(new RequestListingUnlockCommand(id, "let me in")));
    }

    private static Listing Seed(string ownerId)
    {
        var listing = new Listing
        {
            Title = "A place",
            Slug = "a-place",
            Description = "desc",
            Address = "somewhere",
            OwnerId = ownerId,
            Price = new Money(100_000, "TRY"),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 90
        };
        listing.Publish();
        return listing;
    }
}
