namespace RealEstatePortal.Application.Appointments;

// Booking rules in one place so the slot planner, the request command and the UI all agree.
public static class AppointmentPolicy
{
    // Fixed viewing length. One number keeps generated slots and stored appointments aligned.
    public const int SlotDurationMinutes = 60;

    // How far ahead customers can book.
    public const int HorizonDays = 14;

    // A slot must be at least this far in the future — nobody can honour a "in five minutes" booking.
    public static readonly TimeSpan MinimumLead = TimeSpan.FromHours(2);

    // The İstanbul market runs on Türkiye time, which has had no daylight saving since 2016, so a
    // fixed +03:00 offset is exact. Availability is stored as wall-clock TimeOnly and turned into
    // concrete instants at this offset.
    public static readonly TimeSpan MarketOffset = TimeSpan.FromHours(3);
}
