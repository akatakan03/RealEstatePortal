using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.Commands.CancelAppointment;

// Either party calls off a live appointment. Whichever side is signed in determines the cancel
// path so the notification reaches the other one.
public record CancelAppointmentCommand(int AppointmentId) : IRequest;

public class CancelAppointmentCommandHandler : IRequestHandler<CancelAppointmentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CancelAppointmentCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id
            ?? throw new InvalidOperationException("Only a signed-in user can cancel an appointment.");

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.AgentId == userId)
            appointment.CancelByAgent();
        else if (appointment.CustomerId == userId)
            appointment.CancelByCustomer();
        else
            throw new ForbiddenAccessException();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
