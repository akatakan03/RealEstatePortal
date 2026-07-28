using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;

// One weekly window the agent is open for viewings. The editor sends one per day the agent marks
// as available; a day with no window simply isn't in the list.
public record AvailabilityWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End);

// Replaces the signed-in agent's whole weekly availability template in one shot — simpler and less
// error-prone than diffing individual rows, and the set is tiny.
public record SetAgentAvailabilityCommand(IReadOnlyList<AvailabilityWindow> Windows) : IRequest;

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

        var existing = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .ToListAsync(cancellationToken);

        _context.AgentAvailabilities.RemoveRange(existing);

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

        await _context.SaveChangesAsync(cancellationToken);
    }
}
