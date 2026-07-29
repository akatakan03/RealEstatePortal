using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetCustomerAppointments;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class AppointmentReadModelTests
{
    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // ----- Overlap validation -----------------------------------------------------------------

    private static readonly SetAgentAvailabilityCommandValidator Validator = new();
    private static readonly TimeOffEntry[] NoTimeOff = Array.Empty<TimeOffEntry>();

    [Fact]
    public void Validator_RejectsOverlappingWindowsOnTheSameDay()
    {
        var command = new SetAgentAvailabilityCommand(new[]
        {
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(11, 0), new TimeOnly(14, 0))
        }, NoTimeOff);

        Validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_AllowsAdjacentAndSeparateWindows()
    {
        var command = new SetAgentAvailabilityCommand(new[]
        {
            // Adjacent (12:00 touches 12:00) and a separate afternoon block — all fine.
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(12, 0), new TimeOnly(13, 0)),
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(15, 0), new TimeOnly(18, 0))
        }, NoTimeOff);

        Validator.Validate(command).IsValid.ShouldBeTrue();
    }

    // ----- Completed-on-read ------------------------------------------------------------------

    [Fact]
    public async Task PastApprovedAppointment_ReadsAsCompleted()
    {
        // Approved for a time that has already passed relative to "now".
        var past = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.FromHours(3));
        var appt = Appointment.Request(1, "agent-1", "cust-1", past, 60, null);
        appt.Id = 7;
        appt.Approve(null);

        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var listingSet = new List<Listing> { new() { Id = 1, Title = "Ev", Slug = "ev" } }.BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        ctx.Listings.Returns(listingSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns("cust-1");

        var identity = Substitute.For<IIdentityService>();
        identity.GetAgentProfileAsync("agent-1", Arg.Any<CancellationToken>())
            .Returns(new AgentProfileDto("agent-1", "Ada", "ada@x.com", null, null));

        var now = new DateTimeOffset(2026, 8, 3, 6, 0, 0, TimeSpan.FromHours(3));
        var handler = new GetCustomerAppointmentsQueryHandler(ctx, user, identity, new FixedClock(now));

        var result = await handler.Handle(new GetCustomerAppointmentsQuery(), CancellationToken.None);

        var dto = result.Single();
        dto.Status.ShouldBe(AppointmentStatus.Completed);
        dto.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task FutureApprovedAppointment_StaysApproved()
    {
        var future = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.FromHours(3));
        var appt = Appointment.Request(1, "agent-1", "cust-1", future, 60, null);
        appt.Id = 8;
        appt.Approve(null);

        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var listingSet = new List<Listing> { new() { Id = 1, Title = "Ev", Slug = "ev" } }.BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        ctx.Listings.Returns(listingSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns("cust-1");

        var identity = Substitute.For<IIdentityService>();
        identity.GetAgentProfileAsync("agent-1", Arg.Any<CancellationToken>())
            .Returns(new AgentProfileDto("agent-1", "Ada", "ada@x.com", null, null));

        var now = new DateTimeOffset(2026, 8, 3, 6, 0, 0, TimeSpan.FromHours(3));
        var handler = new GetCustomerAppointmentsQueryHandler(ctx, user, identity, new FixedClock(now));

        var result = await handler.Handle(new GetCustomerAppointmentsQuery(), CancellationToken.None);

        result.Single().Status.ShouldBe(AppointmentStatus.Approved);
    }
}
