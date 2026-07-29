using FluentValidation;

namespace RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;

public class SetAgentAvailabilityCommandValidator : AbstractValidator<SetAgentAvailabilityCommand>
{
    public SetAgentAvailabilityCommandValidator()
    {
        RuleForEach(x => x.Windows).ChildRules(w =>
        {
            // A zero-length or inverted window would produce no bookable slots and is almost
            // certainly a mistake. (Several windows on the same day are fine — that's how a midday
            // break is expressed.)
            w.RuleFor(x => x.End)
                .Must((window, end) => end > window.Start)
                .WithMessage("The end time must be after the start time.");
        });

        RuleForEach(x => x.TimeOff).ChildRules(t =>
        {
            // A partial-day exception must be a real range; a whole-day one leaves both blank.
            t.RuleFor(x => x)
                .Must(entry => entry.Start is null || entry.End is null || entry.End > entry.Start)
                .WithMessage("The exception's end time must be after its start time.");
        });
    }
}
