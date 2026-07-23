using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;
using RealEstatePortal.Application.Admin.Commands.PurgeListing;
using RealEstatePortal.Application.Admin.Commands.RestoreDeletedListing;
using RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListing;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingSoftDeleteIntegrationTests : IntegrationTestBase
{
    public ListingSoftDeleteIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Deleting_TakesTheListingOffTheSite_ButKeepsEverythingAttachedToIt()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await SeedListingAsync("Seaside flat", "seaside-flat", withBuyerActivity: true);

        await Fixture.SendAsync(new DeleteListingCommand(id));

        // Gone from every read path: the public list, the detail page, the agent's dashboard.
        var publicList = await Fixture.SendAsync(new GetPublicListingsQuery());
        publicList.Items.ShouldNotContain(l => l.Id == id);

        (await Fixture.SendAsync(new GetListingDetailQuery(id, IncludeNonPublic: true))).ShouldBeNull();

        var dashboard = await Fixture.SendAsync(new GetAgentDashboardQuery());
        dashboard.Listings.Items.ShouldNotContain(r => r.Id == id);
        dashboard.TotalListings.ShouldBe(0);

        // Still all there underneath — this is the whole point of deleting softly.
        await Fixture.ExecuteDbAsync(async db =>
        {
            var row = await db.Listings.IgnoreQueryFilters().SingleAsync(l => l.Id == id);
            row.IsDeleted.ShouldBeTrue();
            row.DeletedBy.ShouldBe("agent-1");

            (await db.Inquiries.CountAsync(i => i.ListingId == id)).ShouldBe(1);
            (await db.Favorites.CountAsync(f => f.ListingId == id)).ShouldBe(1);
            (await db.ListingMedia.CountAsync(m => m.ListingId == id)).ShouldBe(1);
            return 0;
        });

        // And the photos are untouched in storage, because a restore has to be able to
        // bring the listing back whole.
        await Fixture.FileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restoring_BringsItBackAsADraft_WithItsInquiriesIntact()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await SeedListingAsync("City loft", "city-loft", withBuyerActivity: true);
        await Fixture.SendAsync(new DeleteListingCommand(id));

        await Fixture.SendAsync(new RestoreDeletedListingCommand(id));

        var dashboard = await Fixture.SendAsync(new GetAgentDashboardQuery());
        var row = dashboard.Listings.Items.Single(r => r.Id == id);

        // A draft, not straight back onto the public site.
        row.Status.ShouldBe(ListingStatus.Draft);
        row.Inquiries.ShouldBe(1);
        row.Favorites.ShouldBe(1);

        var publicList = await Fixture.SendAsync(new GetPublicListingsQuery());
        publicList.Items.ShouldNotContain(l => l.Id == id);
    }

    [Fact]
    public async Task ANewListing_CanReuseTheTitleOfADeletedOne()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await SeedListingAsync("Sea view apartment", "sea-view-apartment");
        await Fixture.SendAsync(new DeleteListingCommand(id));

        // The deleted row still holds "sea-view-apartment" and the slug index is still unique,
        // so the second listing has to be given a different one rather than failing to insert.
        var newId = await Fixture.SendAsync(new CreateListingCommand
        {
            Title = "Sea view apartment",
            Description = "A different property with the same name.",
            Address = "Kadıköy",
            Price = 2_000_000,
            Currency = "TRY",
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 110
        });

        await Fixture.ExecuteDbAsync(async db =>
        {
            var slug = await db.Listings.Where(l => l.Id == newId).Select(l => l.Slug).SingleAsync();
            slug.ShouldBe("sea-view-apartment-2");
            return 0;
        });
    }

    [Fact]
    public async Task ThePurgeSweep_SkipsAFreshDelete_AndErasesAnExpiredOne()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var fresh = await SeedListingAsync("Fresh", "fresh");
        var expired = await SeedListingAsync("Expired", "expired", withBuyerActivity: true);

        await Fixture.ExecuteDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            (await db.Listings.SingleAsync(l => l.Id == fresh)).Delete(now.AddDays(-1), "agent-1");
            (await db.Listings.SingleAsync(l => l.Id == expired))
                .Delete(now.AddDays(-(ListingDeletion.RetentionDays + 1)), "agent-1");
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var purged = await Fixture.ExecuteScopeAsync(sp =>
            sp.GetRequiredService<IListingPurgeService>().PurgeExpiredAsync(CancellationToken.None));

        purged.ShouldBe(1);

        await Fixture.ExecuteDbAsync(async db =>
        {
            // The one still inside its grace period is untouched.
            (await db.Listings.IgnoreQueryFilters().AnyAsync(l => l.Id == fresh)).ShouldBeTrue();

            // The expired one is gone, and took its dependent rows with it.
            (await db.Listings.IgnoreQueryFilters().AnyAsync(l => l.Id == expired)).ShouldBeFalse();
            (await db.Inquiries.AnyAsync(i => i.ListingId == expired)).ShouldBeFalse();
            (await db.Favorites.AnyAsync(f => f.ListingId == expired)).ShouldBeFalse();
            (await db.ListingMedia.AnyAsync(m => m.ListingId == expired)).ShouldBeFalse();
            return 0;
        });

        // Both objects of the expired listing's one photo are cleared from storage — and
        // nothing belonging to the listing that survived.
        await Fixture.FileStorage.Received(1).DeleteAsync("expired-key", Arg.Any<CancellationToken>());
        await Fixture.FileStorage.Received(1).DeleteAsync("expired-thumb", Arg.Any<CancellationToken>());
        await Fixture.FileStorage.DidNotReceive().DeleteAsync("fresh-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAdministratorCanListTheTrash_AndEraseSomethingBeforeItsTimeIsUp()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await SeedListingAsync("Needs taking down", "needs-taking-down", withBuyerActivity: true);

        Fixture.CurrentUser.Id = "admin-1";
        await Fixture.SendAsync(new AdminDeleteListingCommand(id));

        var trash = await Fixture.SendAsync(new GetListingsForModerationQuery(Deleted: true));
        var listed = trash.Items.Single();
        listed.Id.ShouldBe(id);
        listed.DeletedAt.ShouldNotBeNull();

        (await Fixture.SendAsync(new GetDeletedListingCountQuery())).ShouldBe(1);

        // The normal moderation list never shows it.
        var live = await Fixture.SendAsync(new GetListingsForModerationQuery());
        live.Items.ShouldBeEmpty();

        await Fixture.SendAsync(new PurgeListingCommand(id));

        (await Fixture.SendAsync(new GetDeletedListingCountQuery())).ShouldBe(0);
        await Fixture.ExecuteDbAsync(async db =>
        {
            (await db.Listings.IgnoreQueryFilters().AnyAsync(l => l.Id == id)).ShouldBeFalse();
            (await db.Inquiries.AnyAsync(i => i.ListingId == id)).ShouldBeFalse();
            return 0;
        });
    }

    [Fact]
    public async Task PurgingSomethingThatWasNeverDeleted_DoesNothing()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var id = await SeedListingAsync("Live listing", "live-listing");

        // Guards the one command in the application that destroys data: it acts on deleted
        // listings only, so a stray id can't take a live listing off the site.
        await Fixture.SendAsync(new PurgeListingCommand(id));

        await Fixture.ExecuteDbAsync(async db =>
        {
            (await db.Listings.AnyAsync(l => l.Id == id)).ShouldBeTrue();
            return 0;
        });
        await Fixture.FileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private async Task<int> SeedListingAsync(string title, string slug, bool withBuyerActivity = false)
    {
        return await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = new Listing
            {
                Title = title,
                Slug = slug,
                Description = "desc",
                Address = "İstanbul",
                OwnerId = "agent-1",
                Price = new Money(1_000_000, "TRY"),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                AreaSqMeters = 100
            };
            listing.Publish();
            listing.Media.Add(new ListingMedia
            {
                ObjectKey = $"{slug}-key",
                ThumbnailKey = $"{slug}-thumb",
                IsCover = true
            });

            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            if (withBuyerActivity)
            {
                db.Inquiries.Add(Inquiry.Create(listing.Id, "Buyer", "buyer@x.com", null, "Is it still available?"));
                db.Favorites.Add(new Favorite { ListingId = listing.Id, UserId = "buyer-1" });
                await db.SaveChangesAsync(CancellationToken.None);
            }

            return listing.Id;
        });
    }
}
