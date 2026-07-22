using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ModerationPagingIntegrationTests : IntegrationTestBase
{
    public ModerationPagingIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PagesTheListings_AndResolvesOwnerEmailForThePage()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            for (var i = 0; i < 3; i++)
                db.Listings.Add(Seed($"Listing {i}", $"listing-{i}", "agent-1"));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var page1 = await Fixture.SendAsync(
            new GetListingsForModerationQuery(Status: null, PageNumber: 1, PageSize: 2));

        page1.TotalCount.ShouldBe(3);
        page1.TotalPages.ShouldBe(2);
        page1.Items.Count.ShouldBe(2);
        page1.HasNextPage.ShouldBeTrue();
        page1.HasPreviousPage.ShouldBeFalse();
        page1.Items.First().OwnerEmail.ShouldBe("owner@test.local");   // resolved for the page

        var page2 = await Fixture.SendAsync(
            new GetListingsForModerationQuery(Status: null, PageNumber: 2, PageSize: 2));

        page2.Items.Count.ShouldBe(1);
        page2.HasNextPage.ShouldBeFalse();
        page2.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchFiltersByTitle()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.Listings.Add(Seed("Seaside villa in Bodrum", "seaside-villa", "agent-1"));
            db.Listings.Add(Seed("City flat in Kadıköy", "city-flat", "agent-1"));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var result = await Fixture.SendAsync(
            new GetListingsForModerationQuery(Search: "villa"));

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Title.ShouldBe("Seaside villa in Bodrum");
    }

    private static Listing Seed(string title, string slug, string ownerId)
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
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
