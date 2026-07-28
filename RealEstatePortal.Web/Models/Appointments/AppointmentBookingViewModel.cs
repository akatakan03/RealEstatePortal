using RealEstatePortal.Application.Appointments.Queries.GetAvailableSlots;

namespace RealEstatePortal.Web.Models.Appointments;

// The listing detail page's "book a viewing" panel, loaded on demand.
public class AppointmentBookingViewModel
{
    public int ListingId { get; init; }
    public bool AgentHasAvailability { get; init; }
    public bool IsAuthenticated { get; init; }
    public IReadOnlyList<SlotDay> Days { get; init; } = Array.Empty<SlotDay>();
}
