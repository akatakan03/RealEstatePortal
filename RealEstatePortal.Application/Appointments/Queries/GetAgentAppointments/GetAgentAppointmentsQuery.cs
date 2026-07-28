using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments.Queries.GetAgentAppointments;

// The signed-in agent's viewing requests, newest activity first but with the ones still needing a
// response (pending / counter-proposed) floated to the top.
public record GetAgentAppointmentsQuery : IRequest<IReadOnlyList<AppointmentDto>>;

public class GetAgentAppointmentsQueryHandler
    : IRequestHandler<GetAgentAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identity;

    public GetAgentAppointmentsQueryHandler(
        IApplicationDbContext context, IUser user, IIdentityService identity)
    {
        _context = context;
        _user = user;
        _identity = identity;
    }

    public async Task<IReadOnlyList<AppointmentDto>> Handle(
        GetAgentAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var agentId = _user.Id;
        if (agentId is null) return Array.Empty<AppointmentDto>();

        var rows = await _context.Appointments
            .Where(a => a.AgentId == agentId)
            .Join(_context.Listings,
                a => a.ListingId, l => l.Id,
                (a, l) => new
                {
                    a.Id, a.ListingId, ListingTitle = l.Title, ListingSlug = l.Slug,
                    a.CustomerId, a.Start, a.ProposedStart, a.DurationMinutes,
                    a.Status, a.CustomerNote, a.AgentNote
                })
            .ToListAsync(cancellationToken);

        // The customer's email is the agent's way to reach them; resolve each distinct one once.
        var contacts = await ResolveEmailsAsync(
            rows.Select(r => r.CustomerId).Distinct(), cancellationToken);

        return rows
            .Select(r => new AppointmentDto(
                r.Id, r.ListingId, r.ListingTitle, r.ListingSlug,
                contacts.GetValueOrDefault(r.CustomerId, ""),
                r.Start, r.ProposedStart, r.DurationMinutes, r.Status,
                r.CustomerNote, r.AgentNote,
                IsActive(r.Status)))
            .OrderByDescending(a => NeedsResponse(a.Status))
            .ThenByDescending(a => a.Start)
            .ToList();
    }

    private async Task<Dictionary<string, string>> ResolveEmailsAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>();
        foreach (var id in userIds)
        {
            var email = await _identity.GetUserEmailAsync(id, cancellationToken);
            if (email is not null) map[id] = email;
        }
        return map;
    }

    private static bool NeedsResponse(AppointmentStatus s) => s == AppointmentStatus.Pending;

    private static bool IsActive(AppointmentStatus s) =>
        s is AppointmentStatus.Pending or AppointmentStatus.Approved or AppointmentStatus.CounterProposed;
}
