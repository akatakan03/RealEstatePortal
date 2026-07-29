using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.Commands.RespondToAppointment;

public enum AppointmentAction { Approve, Decline, Propose }

// The agent's response to a pending request: accept it, turn it down, or offer a different time.
public record RespondToAppointmentCommand(
    int AppointmentId, AppointmentAction Action, DateTimeOffset? ProposedStart, string? Note)
    : IRequest;

public class RespondToAppointmentCommandHandler : IRequestHandler<RespondToAppointmentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IAgentScheduleService _schedule;

    public RespondToAppointmentCommandHandler(
        IApplicationDbContext context, IUser user, IAgentScheduleService schedule)
    {
        _context = context;
        _user = user;
        _schedule = schedule;
    }

    public async Task Handle(RespondToAppointmentCommand request, CancellationToken cancellationToken)
    {
        var agentId = _user.Id
            ?? throw new InvalidOperationException("Only a signed-in agent can respond to a request.");

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        // Only the appointment's own agent may respond to it.
        if (appointment.AgentId != agentId)
            throw new ForbiddenAccessException();

        switch (request.Action)
        {
            case AppointmentAction.Approve:
                appointment.Approve(request.Note);
                break;

            case AppointmentAction.Decline:
                appointment.Decline(request.Note);
                break;

            case AppointmentAction.Propose:
                var proposed = request.ProposedStart
                    ?? throw Invalid("Pick a time to propose.");

                // The proposed time must be a real open slot — in the agent's hours, not blocked,
                // and not clashing with another live appointment. This appointment's own current
                // hold is excluded so it doesn't block its own new time.
                var openSlots = await _schedule.GetOpenSlotsAsync(
                    agentId, appointment.Id, cancellationToken);
                if (!openSlots.Contains(proposed))
                    throw Invalid("That time isn't in your availability, or it's already taken.");

                appointment.ProposeNewTime(proposed, request.Note);
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure(nameof(RespondToAppointmentCommand.ProposedStart), message) });
}
