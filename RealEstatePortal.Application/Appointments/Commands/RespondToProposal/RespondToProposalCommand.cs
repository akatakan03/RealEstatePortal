using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.Commands.RespondToProposal;

// The customer's answer to the agent's counter-proposal: accept the new time, or decline it
// (which calls the whole appointment off).
public record RespondToProposalCommand(int AppointmentId, bool Accept) : IRequest;

public class RespondToProposalCommandHandler : IRequestHandler<RespondToProposalCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RespondToProposalCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(RespondToProposalCommand request, CancellationToken cancellationToken)
    {
        var customerId = _user.Id
            ?? throw new InvalidOperationException("Only a signed-in user can answer a proposal.");

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        // Only the appointment's own customer may answer the proposal.
        if (appointment.CustomerId != customerId)
            throw new ForbiddenAccessException();

        if (request.Accept)
            appointment.AcceptProposal();
        else
            appointment.CancelByCustomer(); // declining the proposal calls the viewing off

        await _context.SaveChangesAsync(cancellationToken);
    }
}
