using FluentValidation;

namespace RealEstatePortal.Application.Appointments.Commands.RequestAppointment;

public class RequestAppointmentCommandValidator : AbstractValidator<RequestAppointmentCommand>
{
    public RequestAppointmentCommandValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(1000)
            .WithMessage("Your note is too long.");
    }
}
