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
    private readonly IAgentScheduleService _schedule;

    public GetAvailableSlotsQueryHandler(IApplicationDbContext context, IAgentScheduleService schedule)
    {
        _context = context;
        _schedule = schedule;
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

        // Whether the agent has published any hours at all — drives the "no availability" message.
        var hasWindows = await _context.AgentAvailabilities
            .AnyAsync(a => a.AgentId == listing.OwnerId, cancellationToken);
        if (!hasWindows)
            return new AvailableSlotsResult(false, Array.Empty<SlotDay>());

        var slots = await _schedule.GetOpenSlotsAsync(listing.OwnerId, null, cancellationToken);

        var days = slots
            .GroupBy(s => DateOnly.FromDateTime(s.ToOffset(AppointmentPolicy.MarketOffset).DateTime))
            .Select(g => new SlotDay(g.Key, g.OrderBy(s => s).ToList()))
            .OrderBy(d => d.Date)
            .ToList();

        return new AvailableSlotsResult(true, days);
    }
}
