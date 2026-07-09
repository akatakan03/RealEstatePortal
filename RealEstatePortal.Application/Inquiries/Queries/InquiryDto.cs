using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Inquiries.Queries;

public class InquiryDto
{
    public int Id { get; init; }
    public int ListingId { get; init; }
    public string ListingTitle { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Message { get; init; } = string.Empty;
    public InquiryStatus Status { get; init; }
    public DateTimeOffset Created { get; init; }
}