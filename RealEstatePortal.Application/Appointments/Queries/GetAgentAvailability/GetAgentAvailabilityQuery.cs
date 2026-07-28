using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Appointments.Queries.GetAgentAvailability;

public record AgentAvailabilityDto(DayOfWeek Day, TimeOnly Start, TimeOnly End);

// The signed-in agent's own weekly template, for the availability editor. Ordered Monday-first so
// the editor renders in the natural week order.
public record GetAgentAvailabilityQuery : IRequest<IReadOnlyList<AgentAvailabilityDto>>;

public class GetAgentAvailabilityQueryHandler
    : IRequestHandler<GetAgentAvailabilityQuery, IReadOnlyList<AgentAvailabilityDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetAgentAvailabilityQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<IReadOnlyList<AgentAvailabilityDto>> Handle(
        GetAgentAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var agentId = _user.Id;
        if (agentId is null) return Array.Empty<AgentAvailabilityDto>();

        var rows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .Select(a => new AgentAvailabilityDto(a.DayOfWeek, a.StartTime, a.EndTime))
            .ToListAsync(cancellationToken);

        // Monday-first ordering (DayOfWeek has Sunday = 0).
        return rows
            .OrderBy(a => ((int)a.Day + 6) % 7)
            .ToList();
    }
}
