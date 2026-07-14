using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

public class Favorite : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public int ListingId { get; set; }
}