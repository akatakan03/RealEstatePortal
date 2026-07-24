using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using RealEstatePortal.Application.UnitTests.Common;
using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.UnitTests.Inquiries;

public class CreateInquiryCommandTests
{
    private static Listing ActiveListing(int id = 1, string owner = "agent-1")
    {
        var listing = new Listing { Id = id, Title = "Flat", OwnerId = owner };
        listing.Publish();   // status must be Active for an inquiry to be accepted
        return listing;
    }

    private static CreateInquiryCommand Command(int listingId = 1) => new()
    {
        ListingId = listingId,
        Name = "Buyer",
        Email = "buyer@test.local",
        Phone = "0555",
        Message = "Interested"
    };

    private static (IApplicationDbContext ctx, IEmailService email,
        IIdentityService id, ILogger<CreateInquiryCommandHandler> log) Deps(List<Listing> listings)
    {
        var listingsSet = listings.BuildMockDbSet();
        var inquiriesSet = new List<Inquiry>().BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);
        ctx.Inquiries.Returns(inquiriesSet);

        var email = Substitute.For<IEmailService>();
        var id = Substitute.For<IIdentityService>();
        id.GetEmailRecipientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EmailRecipient("agent@test.local", "tr"));
        var log = Substitute.For<ILogger<CreateInquiryCommandHandler>>();
        return (ctx, email, id, log);
    }

    [Fact]
    public async Task Handle_SavesInquiry_AndEmailsAgent()
    {
        var (ctx, email, id, log) = Deps(new List<Listing> { ActiveListing() });
        var handler = new CreateInquiryCommandHandler(ctx, email, id, new PassThroughText(), log);

        await handler.Handle(Command(), CancellationToken.None);

        ctx.Inquiries.Received(1).Add(Arg.Any<Inquiry>());
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync(
            "agent@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingMissingOrNotActive_ThrowsNotFound()
    {
        var (ctx, email, id, log) = Deps(new List<Listing>());   // empty
        var handler = new CreateInquiryCommandHandler(ctx, email, id, new PassThroughText(), log);

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(Command(99), CancellationToken.None));

        await ctx.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailFails_LeadIsStillSaved()
    {
        var (ctx, email, id, log) = Deps(new List<Listing> { ActiveListing() });

        // Simulate the mail server being down.
        email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));

        var handler = new CreateInquiryCommandHandler(ctx, email, id, new PassThroughText(), log);

        // If the email failure propagated, this await would throw and fail the test.
        await handler.Handle(Command(), CancellationToken.None);

        // The lead was persisted before (and despite) the email attempt.
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}