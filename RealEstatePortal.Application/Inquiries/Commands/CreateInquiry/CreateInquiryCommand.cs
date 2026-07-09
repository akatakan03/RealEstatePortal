using MediatR;

namespace RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;

public class CreateInquiryCommand : IRequest<int>
{
    public int ListingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
}