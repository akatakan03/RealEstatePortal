using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;

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
    private readonly TimeProvider _clock;

    public RespondToAppointmentCommandHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
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
                await EnsureProposableAsync(agentId, appointment.Id, proposed, cancellationToken);
                appointment.ProposeNewTime(proposed, request.Note);
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // The proposed time must be a real open slot for the agent — in their hours and not clashing
    // with any OTHER live appointment (this one's own current hold is naturally excluded).
    private async Task EnsureProposableAsync(
        string agentId, int appointmentId, DateTimeOffset proposed, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var horizonEnd = now.AddDays(AppointmentPolicy.HorizonDays + 1);

        var windows = await _context.AgentAvailabilities
            .Where(a => a.AgentId == agentId)
            .ToListAsync(cancellationToken);

        var live = await _context.Appointments
            .Where(a => a.AgentId == agentId
                && a.Id != appointmentId
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

        if (!SlotPlanner.Generate(windows, busy, now).Contains(proposed))
            throw Invalid("That time isn't in your availability, or it's already taken.");
    }

    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure(nameof(RespondToAppointmentCommand.ProposedStart), message) });
}
