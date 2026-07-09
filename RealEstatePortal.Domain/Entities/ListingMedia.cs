using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

public class ListingMedia : BaseAuditableEntity
{
    public int ListingId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;     // display image key in R2
    public string ThumbnailKey { get; set; } = string.Empty;  // thumbnail key in R2
    public int Order { get; set; }
    public bool IsCover { get; set; }
}