using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetAgentSchedule;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class AgentAvailabilityTests
{
    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static IApplicationDbContext Context(
        List<AgentAvailability> windows, List<AgentTimeOff> timeOff, string? userId, out IUser user)
    {
        var windowSet = windows.BuildMockDbSet();
        var timeOffSet = timeOff.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.AgentAvailabilities.Returns(windowSet);
        ctx.AgentTimeOffs.Returns(timeOffSet);
        user = Substitute.For<IUser>();
        user.Id.Returns(userId);
        return ctx;
    }

    private static readonly TimeOffEntry[] NoTimeOff = Array.Empty<TimeOffEntry>();

    // ----- SetAgentAvailability ---------------------------------------------------------------

    [Fact]
    public async Task Set_ReplacesWindowsAndTimeOff()
    {
        var ctx = Context(
            new List<AgentAvailability>
            {
                new() { Id = 1, AgentId = "agent-1", DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) }
            },
            new List<AgentTimeOff>
            {
                new() { Id = 1, AgentId = "agent-1", Date = new DateOnly(2026, 8, 1) }
            },
            "agent-1", out var user);
        var handler = new SetAgentAvailabilityCommandHandler(ctx, user);

        var command = new SetAgentAvailabilityCommand(
            new[]
            {
                // Two windows on the same day — a midday break — now allowed.
                new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(15, 0), new TimeOnly(18, 0))
            },
            new[]
            {
                new TimeOffEntry(new DateOnly(2026, 8, 6), new TimeOnly(14, 0), null)
            });

        await handler.Handle(command, CancellationToken.None);

        ctx.AgentAvailabilities.Received(1).RemoveRange(Arg.Any<IEnumerable<AgentAvailability>>());
        ctx.AgentTimeOffs.Received(1).RemoveRange(Arg.Any<IEnumerable<AgentTimeOff>>());
        // Both Friday windows added, stamped with the agent.
        ctx.AgentAvailabilities.Received(2).Add(Arg.Is<AgentAvailability>(
            a => a.AgentId == "agent-1" && a.DayOfWeek == DayOfWeek.Friday));
        ctx.AgentTimeOffs.Received(1).Add(Arg.Is<AgentTimeOff>(
            t => t.AgentId == "agent-1" && t.Start == new TimeOnly(14, 0) && t.End == null));
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ----- GetAgentSchedule -------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsWindowsMondayFirst_AndUpcomingTimeOff()
    {
        var now = new DateTimeOffset(2026, 8, 3, 6, 0, 0, TimeSpan.FromHours(3));
        var ctx = Context(
            new List<AgentAvailability>
            {
                new() { AgentId = "agent-1", DayOfWeek = DayOfWeek.Sunday,
                        StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(15, 0) },
                new() { AgentId = "agent-1", DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new() { AgentId = "other", DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) }
            },
            new List<AgentTimeOff>
            {
                new() { AgentId = "agent-1", Date = new DateOnly(2026, 8, 5) },  // upcoming
                new() { AgentId = "agent-1", Date = new DateOnly(2026, 7, 1) }   // past — dropped
            },
            "agent-1", out var user);
        var handler = new GetAgentScheduleQueryHandler(ctx, user, new FixedClock(now));

        var result = await handler.Handle(new GetAgentScheduleQuery(), CancellationToken.None);

        // Only this agent's windows, Monday before Sunday.
        result.Windows.Select(r => r.Day).ShouldBe(new[] { DayOfWeek.Monday, DayOfWeek.Sunday });
        // Only the upcoming exception.
        result.TimeOff.Select(t => t.Date).ShouldBe(new[] { new DateOnly(2026, 8, 5) });
    }

    [Fact]
    public async Task Get_ReturnsEmpty_WhenNotSignedIn()
    {
        var ctx = Context(new List<AgentAvailability>(), new List<AgentTimeOff>(), null, out var user);
        var handler = new GetAgentScheduleQueryHandler(ctx, user, new FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new GetAgentScheduleQuery(), CancellationToken.None);

        result.Windows.ShouldBeEmpty();
        result.TimeOff.ShouldBeEmpty();
    }
}
