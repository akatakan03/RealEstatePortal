using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Application.Appointments.Commands.RequestAppointment;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class RequestAppointmentCommandTests
{
    private static readonly TimeSpan Offset = AppointmentPolicy.MarketOffset;
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 6, 0, 0, Offset);
    private static readonly DateTimeOffset OpenSlot = new(2026, 8, 3, 9, 0, 0, Offset);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static (RequestAppointmentCommandHandler handler, IApplicationDbContext ctx) Build(
        string customerId = "customer-1",
        string ownerId = "agent-1")
    {
        var listing = new Listing { Id = 1, OwnerId = ownerId };
        listing.Publish(); // -> Active

        var listingsSet = new List<Listing> { listing }.BuildMockDbSet();
        var availabilitySet = new List<AgentAvailability>
        {
            new() { AgentId = "agent-1", DayOfWeek = Now.DayOfWeek,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) }
        }.BuildMockDbSet();
        var appointmentsSet = new List<Appointment>().BuildMockDbSet();
        var timeOffSet = new List<AgentTimeOff>().BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);
        ctx.AgentAvailabilities.Returns(availabilitySet);
        ctx.Appointments.Returns(appointmentsSet);
        ctx.AgentTimeOffs.Returns(timeOffSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns(customerId);

        // The real schedule service so the slot gate is exercised end to end.
        var schedule = new AgentScheduleService(ctx, new FixedClock(Now));
        return (new RequestAppointmentCommandHandler(ctx, user, schedule), ctx);
    }

    [Fact]
    public async Task BooksAnOpenSlot()
    {
        var (handler, ctx) = Build();

        await handler.Handle(new RequestAppointmentCommand(1, OpenSlot, "Merhaba"), CancellationToken.None);

        ctx.Appointments.Received(1).Add(Arg.Is<Appointment>(a =>
            a.ListingId == 1 && a.AgentId == "agent-1" && a.CustomerId == "customer-1"
            && a.Start == OpenSlot));
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsATimeThatIsNotAnOpenSlot()
    {
        var (handler, ctx) = Build();

        // 08:00 is before the availability window, so it is not a generated slot.
        var badTime = new DateTimeOffset(2026, 8, 3, 8, 0, 0, Offset);

        await Should.ThrowAsync<ValidationException>(() =>
            handler.Handle(new RequestAppointmentCommand(1, badTime, null), CancellationToken.None));

        ctx.Appointments.DidNotReceive().Add(Arg.Any<Appointment>());
    }

    [Fact]
    public async Task RejectsBookingYourOwnListing()
    {
        // The customer is also the listing's agent.
        var (handler, _) = Build(customerId: "agent-1", ownerId: "agent-1");

        await Should.ThrowAsync<ValidationException>(() =>
            handler.Handle(new RequestAppointmentCommand(1, OpenSlot, null), CancellationToken.None));
    }
}
