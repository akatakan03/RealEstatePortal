using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments;

public class AgentScheduleService : IAgentScheduleService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _clock;

    public AgentScheduleService(IApplicationDbContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetOpenSlotsAsync(
        string agentId, int? excludeAppointmentId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var horizonEnd = now.AddDays(AppointmentPolicy.HorizonDays + 1);

        var windows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .ToListAsync(cancellationToken);

        if (windows.Count == 0)
            return Array.Empty<DateTimeOffset>();

        // The agent's live commitments across ALL their listings — they can't be in two places, so
        // a slot taken for one listing is unavailable for another. A counter-proposed appointment
        // holds its proposed time; a plain pending/approved one holds its own start.
        var live = await _context.Appointments
            .Where(a => a.AgentId == agentId
                && (excludeAppointmentId == null || a.Id != excludeAppointmentId)
                && (a.Status == AppointmentStatus.Pending
                    || a.Status == AppointmentStatus.Approved
                    || a.Status == AppointmentStatus.CounterProposed)
                && a.Start < horizonEnd)
            .Select(a => new { a.Start, a.ProposedStart, a.DurationMinutes, a.Status })
            .ToListAsync(cancellationToken);

        var busy = live.Select(a =>
        {
            var start = a.Status == AppointmentStatus.CounterProposed && a.ProposedStart is not null
                ? a.ProposedStart.Value
                : a.Start;
            return new BusyInterval(start, start.AddMinutes(a.DurationMinutes));
        }).ToList();

        // One-off exceptions: turn each into a busy interval the planner subtracts.
        var offset = AppointmentPolicy.MarketOffset;
        var fromDate = DateOnly.FromDateTime(now.ToOffset(offset).DateTime);
        var toDate = fromDate.AddDays(AppointmentPolicy.HorizonDays);

        var timeOff = await _context.AgentTimeOffs
            .Where(t => t.AgentId == agentId && t.Date >= fromDate && t.Date <= toDate)
            .Select(t => new { t.Date, t.Start, t.End })
            .ToListAsync(cancellationToken);

        foreach (var t in timeOff)
        {
            var dayStart = new DateTimeOffset(t.Date.Year, t.Date.Month, t.Date.Day, 0, 0, 0, offset);
            var start = t.Start.HasValue
                ? new DateTimeOffset(t.Date.Year, t.Date.Month, t.Date.Day, t.Start.Value.Hour, t.Start.Value.Minute, 0, offset)
                : dayStart;
            var end = t.End.HasValue
                ? new DateTimeOffset(t.Date.Year, t.Date.Month, t.Date.Day, t.End.Value.Hour, t.End.Value.Minute, 0, offset)
                : dayStart.AddDays(1); // no end time → blocked to the end of the day
            busy.Add(new BusyInterval(start, end));
        }

        return SlotPlanner.Generate(windows, busy, now);
    }
}
