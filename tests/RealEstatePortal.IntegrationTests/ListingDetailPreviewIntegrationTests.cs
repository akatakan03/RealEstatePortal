using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingDetailPreviewIntegrationTests : IntegrationTestBase
{
    public ListingDetailPreviewIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PublicQuery_HidesADraft_ButPreviewReturnsIt()
    {
        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var draft = new Listing
            {
                Title = "Not live yet",
                Slug = "not-live-yet",
                Description = "desc",
                Address = "somewhere",
                OwnerId = "agent-1",
                Price = new Money(100_000, "TRY"),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                AreaSqMeters = 90
            };
            // deliberately NOT published -> stays Draft
            db.Listings.Add(draft);
            await db.SaveChangesAsync(CancellationToken.None);
            return draft.Id;
        });

        // The public site sees only Active listings.
        (await Fixture.SendAsync(new GetListingDetailQuery(id))).ShouldBeNull();

        // A privileged caller can preview it.
        var preview = await Fixture.SendAsync(new GetListingDetailQuery(id, IncludeNonPublic: true));
        preview.ShouldNotBeNull();
        preview!.Status.ShouldBe(ListingStatus.Draft);
    }
}
