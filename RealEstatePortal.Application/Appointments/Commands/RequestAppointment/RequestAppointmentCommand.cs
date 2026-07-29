using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments.Commands.RequestAppointment;

// A logged-in customer books one of the listing's open slots. Returns the new appointment id.
public record RequestAppointmentCommand(int ListingId, DateTimeOffset Start, string? Note)
    : IRequest<int>;

public class RequestAppointmentCommandHandler : IRequestHandler<RequestAppointmentCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IAgentScheduleService _schedule;

    public RequestAppointmentCommandHandler(
        IApplicationDbContext context, IUser user, IAgentScheduleService schedule)
    {
        _context = context;
        _user = user;
        _schedule = schedule;
    }

    public async Task<int> Handle(RequestAppointmentCommand request, CancellationToken cancellationToken)
    {
        var customerId = _user.Id
            ?? throw new InvalidOperationException("Only a signed-in user can book a viewing.");

        var listing = await _context.Listings
            .Where(l => l.Id == request.ListingId && l.Status == ListingStatus.Active)
            .Select(l => new { l.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.ListingId);
        if (listing.OwnerId is null)
            throw Invalid("This listing has no agent to meet with.");

        var agentId = listing.OwnerId;

        // An agent booking a viewing on their own listing is almost certainly a mistake.
        if (agentId == customerId)
            throw Invalid("You can't book a viewing on your own listing.");

        // Re-derive the open slots on the server and require the requested time to be one of them.
        // This is the real gate: it rejects past, taken, blocked, or hand-crafted times regardless
        // of the UI.
        var openSlots = await _schedule.GetOpenSlotsAsync(agentId, null, cancellationToken);
        if (!openSlots.Contains(request.Start))
            throw Invalid("That time is no longer available. Please pick another slot.");

        var appointment = Appointment.Request(
            request.ListingId, agentId, customerId,
            request.Start, AppointmentPolicy.SlotDurationMinutes, request.Note);

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync(cancellationToken);

        return appointment.Id;
    }

    // The app's ValidationException carries FluentValidation failures; wrap a single user-facing
    // message as one so it surfaces the same way as rule violations.
    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure(nameof(RequestAppointmentCommand.Start), message) });
}
