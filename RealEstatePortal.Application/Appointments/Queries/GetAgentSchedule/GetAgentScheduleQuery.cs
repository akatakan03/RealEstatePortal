using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Appointments.Queries.GetAgentSchedule;

public record AgentAvailabilityDto(DayOfWeek Day, TimeOnly Start, TimeOnly End);

public record TimeOffDto(DateOnly Date, TimeOnly? Start, TimeOnly? End);

public record AgentScheduleDto(
    IReadOnlyList<AgentAvailabilityDto> Windows,
    IReadOnlyList<TimeOffDto> TimeOff);

// The signed-in agent's own schedule for the editor: recurring weekly windows plus upcoming date
// exceptions. Windows come Monday-first; exceptions come soonest-first and past ones are dropped.
public record GetAgentScheduleQuery : IRequest<AgentScheduleDto>;

public class GetAgentScheduleQueryHandler : IRequestHandler<GetAgentScheduleQuery, AgentScheduleDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;

    public GetAgentScheduleQueryHandler(IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
    }

    public async Task<AgentScheduleDto> Handle(
        GetAgentScheduleQuery request, CancellationToken cancellationToken)
    {
        var agentId = _user.Id;
        if (agentId is null)
            return new AgentScheduleDto(Array.Empty<AgentAvailabilityDto>(), Array.Empty<TimeOffDto>());

        var windowRows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .Select(a => new AgentAvailabilityDto(a.DayOfWeek, a.StartTime, a.EndTime))
            .ToListAsync(cancellationToken);

        // Monday-first, then by start time so a day's windows read top to bottom.
        var windows = windowRows
            .OrderBy(a => ((int)a.Day + 6) % 7)
            .ThenBy(a => a.Start)
            .ToList();

        var today = DateOnly.FromDateTime(
            _clock.GetUtcNow().ToOffset(AppointmentPolicy.MarketOffset).DateTime);

        var timeOff = await _context.AgentTimeOffs
            .Where(t => t.AgentId == agentId && t.Date >= today)
            .OrderBy(t => t.Date)
            .Select(t => new TimeOffDto(t.Date, t.Start, t.End))
            .ToListAsync(cancellationToken);

        return new AgentScheduleDto(windows, timeOff);
    }
}
