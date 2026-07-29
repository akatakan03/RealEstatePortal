using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Application.Appointments.Commands.CancelAppointment;
using RealEstatePortal.Application.Appointments.Commands.RespondToAppointment;
using RealEstatePortal.Application.Appointments.Commands.RespondToProposal;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class AppointmentResponseTests
{
    private static readonly DateTimeOffset Slot =
        new(2026, 8, 3, 9, 0, 0, TimeSpan.FromHours(3));

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static Appointment Pending(int id = 5, string agent = "agent-1", string customer = "cust-1")
    {
        var a = Appointment.Request(1, agent, customer, Slot, 60, "hi");
        a.Id = id;
        return a;
    }

    private static (RespondToAppointmentCommandHandler handler, Appointment appt) BuildRespond(
        Appointment appt, string userId)
    {
        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var availSet = new List<AgentAvailability>().BuildMockDbSet();
        var timeOffSet = new List<AgentTimeOff>().BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        ctx.AgentAvailabilities.Returns(availSet);
        ctx.AgentTimeOffs.Returns(timeOffSet);
        var user = Substitute.For<IUser>();
        user.Id.Returns(userId);
        var schedule = new AgentScheduleService(
            ctx, new FixedClock(new DateTimeOffset(2026, 8, 3, 6, 0, 0, TimeSpan.FromHours(3))));
        return (new RespondToAppointmentCommandHandler(ctx, user, schedule), appt);
    }

    // ----- Agent responses --------------------------------------------------------------------

    [Fact]
    public async Task Approve_MovesToApproved()
    {
        var (handler, appt) = BuildRespond(Pending(), "agent-1");

        await handler.Handle(
            new RespondToAppointmentCommand(5, AppointmentAction.Approve, null, "See you"),
            CancellationToken.None);

        appt.Status.ShouldBe(AppointmentStatus.Approved);
    }

    [Fact]
    public async Task Respond_ByWrongAgent_IsForbidden()
    {
        var (handler, _) = BuildRespond(Pending(agent: "agent-1"), "someone-else");

        await Should.ThrowAsync<ForbiddenAccessException>(() => handler.Handle(
            new RespondToAppointmentCommand(5, AppointmentAction.Approve, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task Propose_ATimeOutsideAvailability_IsRejected()
    {
        // No availability rows, so no time can be a valid slot.
        var (handler, appt) = BuildRespond(Pending(), "agent-1");

        await Should.ThrowAsync<ValidationException>(() => handler.Handle(
            new RespondToAppointmentCommand(5, AppointmentAction.Propose, Slot.AddDays(1), null),
            CancellationToken.None));

        appt.Status.ShouldBe(AppointmentStatus.Pending); // unchanged
    }

    // ----- Customer answer to a proposal ------------------------------------------------------

    private static Appointment CounterProposed(DateTimeOffset proposed)
    {
        var a = Pending();
        a.ProposeNewTime(proposed, "How about this?");
        return a;
    }

    [Fact]
    public async Task AcceptProposal_ConfirmsAtTheProposedTime()
    {
        var proposed = Slot.AddDays(2);
        var appt = CounterProposed(proposed);
        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        var user = Substitute.For<IUser>();
        user.Id.Returns("cust-1");
        var handler = new RespondToProposalCommandHandler(ctx, user);

        await handler.Handle(new RespondToProposalCommand(5, Accept: true), CancellationToken.None);

        appt.Status.ShouldBe(AppointmentStatus.Approved);
        appt.Start.ShouldBe(proposed);
    }

    [Fact]
    public async Task DeclineProposal_CallsItOff()
    {
        var appt = CounterProposed(Slot.AddDays(2));
        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        var user = Substitute.For<IUser>();
        user.Id.Returns("cust-1");
        var handler = new RespondToProposalCommandHandler(ctx, user);

        await handler.Handle(new RespondToProposalCommand(5, Accept: false), CancellationToken.None);

        appt.Status.ShouldBe(AppointmentStatus.CancelledByCustomer);
    }

    // ----- Cancellation -----------------------------------------------------------------------

    [Fact]
    public async Task Cancel_ByAgent_AndByCustomer_UseTheRightSide()
    {
        foreach (var (userId, expected) in new[]
        {
            ("agent-1", AppointmentStatus.CancelledByAgent),
            ("cust-1", AppointmentStatus.CancelledByCustomer)
        })
        {
            var appt = Pending();
            var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
            var ctx = Substitute.For<IApplicationDbContext>();
            ctx.Appointments.Returns(apptSet);
            var user = Substitute.For<IUser>();
            user.Id.Returns(userId);
            var handler = new CancelAppointmentCommandHandler(ctx, user);

            await handler.Handle(new CancelAppointmentCommand(5), CancellationToken.None);

            appt.Status.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task Cancel_ByStranger_IsForbidden()
    {
        var appt = Pending();
        var apptSet = new List<Appointment> { appt }.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Appointments.Returns(apptSet);
        var user = Substitute.For<IUser>();
        user.Id.Returns("stranger");
        var handler = new CancelAppointmentCommandHandler(ctx, user);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            handler.Handle(new CancelAppointmentCommand(5), CancellationToken.None));
    }
}
