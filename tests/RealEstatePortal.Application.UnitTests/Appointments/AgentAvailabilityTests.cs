using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetAgentAvailability;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class AgentAvailabilityTests
{
    private static (T handler, IApplicationDbContext ctx) BuildWith<T>(
        List<AgentAvailability> rows, string? userId, Func<IApplicationDbContext, IUser, T> make)
    {
        var set = rows.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.AgentAvailabilities.Returns(set);
        var user = Substitute.For<IUser>();
        user.Id.Returns(userId);
        return (make(ctx, user), ctx);
    }

    // ----- SetAgentAvailability ---------------------------------------------------------------

    [Fact]
    public async Task Set_ReplacesTheAgentsExistingWindows()
    {
        var existing = new List<AgentAvailability>
        {
            new() { Id = 1, AgentId = "agent-1", DayOfWeek = DayOfWeek.Monday,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) }
        };
        var (handler, ctx) = BuildWith(existing, "agent-1",
            (c, u) => new SetAgentAvailabilityCommandHandler(c, u));

        var command = new SetAgentAvailabilityCommand(new[]
        {
            new AvailabilityWindow(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
            new AvailabilityWindow(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0))
        });

        await handler.Handle(command, CancellationToken.None);

        // Old rows cleared, new ones added, and persisted once.
        ctx.AgentAvailabilities.Received(1).RemoveRange(Arg.Any<IEnumerable<AgentAvailability>>());
        ctx.AgentAvailabilities.Received(1).Add(Arg.Is<AgentAvailability>(
            a => a.AgentId == "agent-1" && a.DayOfWeek == DayOfWeek.Tuesday));
        ctx.AgentAvailabilities.Received(1).Add(Arg.Is<AgentAvailability>(
            a => a.DayOfWeek == DayOfWeek.Thursday));
        await ctx.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_StampsTheCurrentAgentOnEachWindow()
    {
        var (handler, ctx) = BuildWith(new List<AgentAvailability>(), "agent-9",
            (c, u) => new SetAgentAvailabilityCommandHandler(c, u));

        await handler.Handle(new SetAgentAvailabilityCommand(new[]
        {
            new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(18, 0))
        }), CancellationToken.None);

        ctx.AgentAvailabilities.Received(1).Add(Arg.Is<AgentAvailability>(a => a.AgentId == "agent-9"));
    }

    // ----- GetAgentAvailability ---------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsTheAgentsWindows_MondayFirst()
    {
        var rows = new List<AgentAvailability>
        {
            new() { AgentId = "agent-1", DayOfWeek = DayOfWeek.Sunday,
                    StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(15, 0) },
            new() { AgentId = "agent-1", DayOfWeek = DayOfWeek.Monday,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
            new() { AgentId = "other", DayOfWeek = DayOfWeek.Monday,
                    StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) }
        };
        var (handler, _) = BuildWith(rows, "agent-1",
            (c, u) => new GetAgentAvailabilityQueryHandler(c, u));

        var result = await handler.Handle(new GetAgentAvailabilityQuery(), CancellationToken.None);

        // Only this agent's rows, Monday before Sunday.
        result.Select(r => r.Day).ShouldBe(new[] { DayOfWeek.Monday, DayOfWeek.Sunday });
    }

    [Fact]
    public async Task Get_ReturnsEmpty_WhenNotSignedIn()
    {
        var (handler, _) = BuildWith(new List<AgentAvailability>(), null,
            (c, u) => new GetAgentAvailabilityQueryHandler(c, u));

        var result = await handler.Handle(new GetAgentAvailabilityQuery(), CancellationToken.None);

        result.ShouldBeEmpty();
    }
}
