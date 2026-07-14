using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;

namespace RealEstatePortal.Web.Models.Listings;

public class ListingDetailViewModel
{
    public ListingDetailDto Listing { get; set; } = default!;
    public CreateInquiryCommand Inquiry { get; set; } = new();
    public bool IsFavorited { get; set; }
}