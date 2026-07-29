using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;

// One weekly window the agent is open for viewings. A day can have several — e.g. 09:00–12:00 and
// 15:00–18:00 leaves a midday gap.
public record AvailabilityWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End);

// A one-off exception on a specific date. Start/End are optional and read as a blocked range
// (see AgentTimeOff): both null = whole day off; Start only = "after X"; End only = "before X".
public record TimeOffEntry(DateOnly Date, TimeOnly? Start, TimeOnly? End);

// Replaces the signed-in agent's whole schedule — weekly windows and date exceptions — in one shot.
// Simpler and less error-prone than diffing individual rows, and the set is small.
public record SetAgentAvailabilityCommand(
    IReadOnlyList<AvailabilityWindow> Windows,
    IReadOnlyList<TimeOffEntry> TimeOff) : IRequest;

public class SetAgentAvailabilityCommandHandler : IRequestHandler<SetAgentAvailabilityCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SetAgentAvailabilityCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(SetAgentAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var agentId = _user.Id
            ?? throw new InvalidOperationException("Only a signed-in agent can set availability.");

        var existingWindows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .ToListAsync(cancellationToken);
        _context.AgentAvailabilities.RemoveRange(existingWindows);

        foreach (var w in request.Windows)
        {
            _context.AgentAvailabilities.Add(new AgentAvailability
            {
                AgentId = agentId,
                DayOfWeek = w.Day,
                StartTime = w.Start,
                EndTime = w.End
            });
        }

        var existingTimeOff = await _context.AgentTimeOffs
            .Where(t => t.AgentId == agentId)
            .ToListAsync(cancellationToken);
        _context.AgentTimeOffs.RemoveRange(existingTimeOff);

        foreach (var t in request.TimeOff)
        {
            _context.AgentTimeOffs.Add(new AgentTimeOff
            {
                AgentId = agentId,
                Date = t.Date,
                Start = t.Start,
                End = t.End
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
