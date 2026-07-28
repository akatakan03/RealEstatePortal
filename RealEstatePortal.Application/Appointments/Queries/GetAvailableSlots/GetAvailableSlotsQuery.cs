using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments.Queries.GetAvailableSlots;

public record SlotDay(DateOnly Date, IReadOnlyList<DateTimeOffset> Slots);

// AgentHasAvailability distinguishes "the agent hasn't set any hours" (tell the customer to try the
// message form) from "hours exist but every upcoming slot is taken" (Days is empty).
public record AvailableSlotsResult(bool AgentHasAvailability, IReadOnlyList<SlotDay> Days);

// Open viewing slots for a listing over the booking horizon. Pure database work, so it's cheap
// enough to load inline. Returns null when the listing isn't a bookable (active) listing.
public record GetAvailableSlotsQuery(int ListingId) : IRequest<AvailableSlotsResult?>;

public class GetAvailableSlotsQueryHandler
    : IRequestHandler<GetAvailableSlotsQuery, AvailableSlotsResult?>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _clock;

    public GetAvailableSlotsQueryHandler(IApplicationDbContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<AvailableSlotsResult?> Handle(
        GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Where(l => l.Id == request.ListingId && l.Status == ListingStatus.Active)
            .Select(l => new { l.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null || listing.OwnerId is null)
            return listing is null ? null : new AvailableSlotsResult(false, Array.Empty<SlotDay>());

        var agentId = listing.OwnerId;

        var windows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .ToListAsync(cancellationToken);

        if (windows.Count == 0)
            return new AvailableSlotsResult(false, Array.Empty<SlotDay>());

        var now = _clock.GetUtcNow();
        var horizonEnd = now.AddDays(AppointmentPolicy.HorizonDays + 1);

        // The agent's live commitments across ALL their listings — they can't be in two places, so
        // a slot taken for one listing is unavailable for another. A counter-proposed appointment
        // holds the agent's proposed time; a plain pending/approved one holds its own start.
        var live = await _context.Appointments
            .Where(a => a.AgentId == agentId
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
        });

        var slots = SlotPlanner.Generate(windows, busy, now);

        var days = slots
            .GroupBy(s => DateOnly.FromDateTime(s.ToOffset(AppointmentPolicy.MarketOffset).DateTime))
            .Select(g => new SlotDay(g.Key, g.OrderBy(s => s).ToList()))
            .OrderBy(d => d.Date)
            .ToList();

        return new AvailableSlotsResult(true, days);
    }
}
