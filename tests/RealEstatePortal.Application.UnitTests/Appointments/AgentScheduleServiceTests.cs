using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class AgentScheduleServiceTests
{
    private static readonly TimeSpan Offset = AppointmentPolicy.MarketOffset;
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 6, 0, 0, Offset);
    private static readonly DateOnly Today = new(2026, 8, 3);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static AgentScheduleService Build(List<AgentTimeOff> timeOff)
    {
        // Build each mock DbSet into a local first: BuildMockDbSet configures its own substitute,
        // and doing that inside a .Returns(...) argument clobbers NSubstitute's last-call context.
        var windowSet = new List<AgentAvailability>
        {
            new() { AgentId = "agent-1", DayOfWeek = Now.DayOfWeek,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) }
        }.BuildMockDbSet();
        var appointmentSet = new List<Appointment>().BuildMockDbSet();
        var timeOffSet = timeOff.BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.AgentAvailabilities.Returns(windowSet);
        ctx.Appointments.Returns(appointmentSet);
        ctx.AgentTimeOffs.Returns(timeOffSet);

        return new AgentScheduleService(ctx, new FixedClock(Now));
    }

    // Slots on the first horizon day (today), by hour.
    private static List<int> TodayHours(IEnumerable<DateTimeOffset> slots) =>
        slots.Where(s => DateOnly.FromDateTime(s.ToOffset(Offset).DateTime) == Today)
             .Select(s => s.ToOffset(Offset).Hour)
             .ToList();

    [Fact]
    public async Task WithoutExceptions_TheWholeWindowIsOpen()
    {
        var service = Build(new List<AgentTimeOff>());

        var slots = await service.GetOpenSlotsAsync("agent-1", null, CancellationToken.None);

        TodayHours(slots).ShouldBe(new[] { 9, 10, 11 });
    }

    [Fact]
    public async Task AnAfterTimeException_BlocksTheRestOfThatDay()
    {
        // "Not available after 10:00" today → 10:00 and 11:00 slots disappear, 09:00 stays.
        var service = Build(new List<AgentTimeOff>
        {
            new() { AgentId = "agent-1", Date = Today, Start = new TimeOnly(10, 0), End = null }
        });

        var slots = await service.GetOpenSlotsAsync("agent-1", null, CancellationToken.None);

        TodayHours(slots).ShouldBe(new[] { 9 });
    }

    [Fact]
    public async Task AWholeDayException_BlocksEveryStreamSlotThatDay()
    {
        var service = Build(new List<AgentTimeOff>
        {
            new() { AgentId = "agent-1", Date = Today, Start = null, End = null }
        });

        var slots = await service.GetOpenSlotsAsync("agent-1", null, CancellationToken.None);

        TodayHours(slots).ShouldBeEmpty();
    }
}
