using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Domain.Entities;

public class Inquiry : BaseAuditableEntity
{
    public int ListingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
    public InquiryStatus Status { get; private set; } = InquiryStatus.New;

    public void MarkAsRead() => Status = InquiryStatus.Read;
    public void MarkAsHandled() => Status = InquiryStatus.Handled;
}