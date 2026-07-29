using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments.Queries.GetCustomerAppointments;

// The signed-in customer's viewing requests, with the ones needing their attention (a pending
// counter-proposal) floated to the top.
public record GetCustomerAppointmentsQuery : IRequest<IReadOnlyList<AppointmentDto>>;

public class GetCustomerAppointmentsQueryHandler
    : IRequestHandler<GetCustomerAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identity;
    private readonly TimeProvider _clock;

    public GetCustomerAppointmentsQueryHandler(
        IApplicationDbContext context, IUser user, IIdentityService identity, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _identity = identity;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AppointmentDto>> Handle(
        GetCustomerAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _user.Id;
        if (customerId is null) return Array.Empty<AppointmentDto>();

        var rows = await _context.Appointments
            .Where(a => a.CustomerId == customerId)
            .Join(_context.Listings,
                a => a.ListingId, l => l.Id,
                (a, l) => new
                {
                    a.Id, a.ListingId, ListingTitle = l.Title, ListingSlug = l.Slug,
                    a.AgentId, a.Start, a.ProposedStart, a.DurationMinutes,
                    a.Status, a.CustomerNote, a.AgentNote
                })
            .ToListAsync(cancellationToken);

        // Show the agent by display name (falling back to email) — that's who the customer is
        // meeting.
        var names = await ResolveAgentNamesAsync(
            rows.Select(r => r.AgentId).Distinct(), cancellationToken);

        var now = _clock.GetUtcNow();

        return rows
            .Select(r =>
            {
                var status = Effective(r.Status, r.Start, r.DurationMinutes, now);
                return new AppointmentDto(
                    r.Id, r.ListingId, r.ListingTitle, r.ListingSlug,
                    names.GetValueOrDefault(r.AgentId, ""),
                    r.Start, r.ProposedStart, r.DurationMinutes, status,
                    r.CustomerNote, r.AgentNote,
                    IsActive(status));
            })
            .OrderByDescending(a => NeedsResponse(a.Status))
            .ThenByDescending(a => a.Start)
            .ToList();
    }

    // An approved appointment whose time has passed reads as Completed, without a background job
    // ever having to write that status to the row.
    private static AppointmentStatus Effective(
        AppointmentStatus status, DateTimeOffset start, int durationMinutes, DateTimeOffset now) =>
        status == AppointmentStatus.Approved && start.AddMinutes(durationMinutes) <= now
            ? AppointmentStatus.Completed
            : status;

    private async Task<Dictionary<string, string>> ResolveAgentNamesAsync(
        IEnumerable<string> agentIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>();
        foreach (var id in agentIds)
        {
            var profile = await _identity.GetAgentProfileAsync(id, cancellationToken);
            if (profile is not null)
                map[id] = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Email : profile.DisplayName;
        }
        return map;
    }

    // For the customer, the counter-proposal is the item that needs their decision.
    private static bool NeedsResponse(AppointmentStatus s) => s == AppointmentStatus.CounterProposed;

    private static bool IsActive(AppointmentStatus s) =>
        s is AppointmentStatus.Pending or AppointmentStatus.Approved or AppointmentStatus.CounterProposed;
}
