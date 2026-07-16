using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Agents.Queries.GetAgentPublicProfile;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Agents;

public class GetAgentPublicProfileQueryTests
{
    private static Listing ListingFor(string ownerId, int id, ListingStatus status)
    {
        var listing = new Listing
        {
            Id = id,
            Title = $"Listing {id}",
            Slug = $"listing-{id}",
            OwnerId = ownerId,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Bedrooms = 2,
            AreaSqMeters = 80,
            Price = new Money(1_000_000m, "TRY")
        };

        // Status is set only through domain methods (private setter) — drive it there.
        switch (status)
        {
            case ListingStatus.Active:
                listing.Publish();
                break;
            case ListingStatus.Archived:
                listing.Publish();
                listing.Archive();
                break;
            case ListingStatus.Draft:
                // new listings already start as Draft — nothing to do
                break;
        }

        return listing;
    }

    private static GetAgentPublicProfileQueryHandler Build(
        List<Listing> listings, AgentProfileDto? profile)
    {
        var listingsSet = listings.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);

        var identity = Substitute.For<IIdentityService>();
        identity.GetAgentProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(profile);

        var storage = Substitute.For<IFileStorageService>();
        storage.GetPublicUrl(Arg.Any<string>()).Returns(ci => $"https://cdn/{ci.Arg<string>()}");

        return new GetAgentPublicProfileQueryHandler(ctx, identity, storage);
    }

    [Fact]
    public async Task WhenUserIsNotAnAgent_ReturnsNull()
    {
        // IIdentityService returns null for non-agents (its own role check).
        var handler = Build(new List<Listing>(), profile: null);

        var result = await handler.Handle(new GetAgentPublicProfileQuery("member-1"), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsOnlyTheAgentsActiveListings()
    {
        var profile = new AgentProfileDto("agent-1", "Ada Agent", "ada@test.local", "Bio", null);
        var listings = new List<Listing>
        {
            ListingFor("agent-1", 1, ListingStatus.Active),
            ListingFor("agent-1", 2, ListingStatus.Draft),      // hidden — not active
            ListingFor("agent-1", 3, ListingStatus.Archived),   // hidden — not active
            ListingFor("agent-2", 4, ListingStatus.Active)      // hidden — different agent
        };
        var handler = Build(listings, profile);

        var result = await handler.Handle(new GetAgentPublicProfileQuery("agent-1"), CancellationToken.None);

        result.ShouldNotBeNull();
        result!.DisplayName.ShouldBe("Ada Agent");
        result.Listings.Count.ShouldBe(1);          // only the active one owned by agent-1
        result.Listings[0].Id.ShouldBe(1);
    }

    [Fact]
    public async Task FallsBackToEmail_WhenNoDisplayName()
    {
        var profile = new AgentProfileDto("agent-1", null, "ada@test.local", null, null);
        var handler = Build(new List<Listing>(), profile);

        var result = await handler.Handle(new GetAgentPublicProfileQuery("agent-1"), CancellationToken.None);

        result!.DisplayName.ShouldBe("ada@test.local");   // email stands in for a missing name
    }
}