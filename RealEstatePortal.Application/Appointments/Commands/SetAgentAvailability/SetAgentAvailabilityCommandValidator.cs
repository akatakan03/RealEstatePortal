using FluentValidation;

namespace RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;

public class SetAgentAvailabilityCommandValidator : AbstractValidator<SetAgentAvailabilityCommand>
{
    public SetAgentAvailabilityCommandValidator()
    {
        RuleForEach(x => x.Windows).ChildRules(w =>
        {
            // A zero-length or inverted window would produce no bookable slots and is almost
            // certainly a mistake, so reject it rather than silently storing it.
            w.RuleFor(x => x.End)
                .Must((window, end) => end > window.Start)
                .WithMessage("The end time must be after the start time.");
        });

        // A day can only be open once — the editor offers one window per day, so a duplicate day
        // means something is off.
        RuleFor(x => x.Windows)
            .Must(windows => windows.Select(w => w.Day).Distinct().Count() == windows.Count)
            .WithMessage("Each day can have only one availability window.");
    }
}
