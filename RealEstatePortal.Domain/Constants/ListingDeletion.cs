namespace RealEstatePortal.Domain.Constants;

public static class ListingDeletion
{
    // How long a deleted listing stays restorable before it and its photos are erased.
    // A product rule rather than a deployment setting, so it lives here and every layer
    // that has to state it — the purge sweep, the agent's confirmation screen, the trash
    // view — quotes the same number.
    public const int RetentionDays = 30;
}
