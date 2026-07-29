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

        // Two ranges on the same day must not overlap — otherwise the same hour would be offered as
        // a slot twice. Adjacent ranges (one ends where the next begins) are fine.
        RuleFor(x => x.Windows)
            .Must(NoOverlappingWindows)
            .WithMessage("Two time ranges on the same day overlap.");

        RuleForEach(x => x.TimeOff).ChildRules(t =>
        {
            // A partial-day exception must be a real range; a whole-day one leaves both blank.
            t.RuleFor(x => x)
                .Must(entry => entry.Start is null || entry.End is null || entry.End > entry.Start)
                .WithMessage("The exception's end time must be after its start time.");
        });
    }

    private static bool NoOverlappingWindows(IReadOnlyList<AvailabilityWindow> windows)
    {
        foreach (var day in windows.GroupBy(w => w.Day))
        {
            var ordered = day.OrderBy(w => w.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].Start < ordered[i - 1].End) // starts before the previous one ends
                    return false;
        }
        return true;
    }
}
