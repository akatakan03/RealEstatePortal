namespace RealEstatePortal.Application.Appointments;

// Computes an agent's bookable slots from their weekly template, minus their live appointments and
// their one-off time-off exceptions. One place so the slot picker, the booking command and the
// counter-proposal command all agree on what "open" means.
public interface IAgentScheduleService
{
    // excludeAppointmentId lets a counter-proposal ignore the appointment being moved, so its own
    // current hold doesn't block the new time.
    Task<IReadOnlyList<DateTimeOffset>> GetOpenSlotsAsync(
        string agentId, int? excludeAppointmentId, CancellationToken cancellationToken = default);
}
