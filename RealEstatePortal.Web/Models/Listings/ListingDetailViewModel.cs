using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetListings;

namespace RealEstatePortal.Web.Models.Listings;

public class ListingDetailViewModel
{
    public ListingDetailDto Listing { get; set; } = default!;
    public CreateInquiryCommand Inquiry { get; set; } = new();
    public bool IsFavorited { get; set; }

    // "More like this" — a few active listings a buyer looking at this one might consider next.
    // Empty when nothing similar enough turns up, in which case the view hides the whole section.
    public IReadOnlyList<ListingBriefDto> Similar { get; set; } = Array.Empty<ListingBriefDto>();

    // Seeds the loan calculator (sale listings only). The live EVDS sector average when available,
    // otherwise the configured fallback — the service guarantees a usable number.
    public decimal MortgageMonthlyRate { get; set; }
}