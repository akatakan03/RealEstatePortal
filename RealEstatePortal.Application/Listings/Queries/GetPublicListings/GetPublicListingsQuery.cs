using MediatR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetPublicListings;

public record GetPublicListingsQuery : IRequest<PaginatedList<ListingBriefDto>>
{
    public string? Keyword { get; set; }
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

public class GetPublicListingsQueryHandler
    : IRequestHandler<GetPublicListingsQuery, PaginatedList<ListingBriefDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicListingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ListingBriefDto>> Handle(
        GetPublicListingsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings
            .Where(l => l.Status == ListingStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(l => l.Title.Contains(kw) || l.Address.Contains(kw));
        }
        if (request.ListingType.HasValue)
            query = query.Where(l => l.ListingType == request.ListingType.Value);
        if (request.PropertyType.HasValue)
            query = query.Where(l => l.PropertyType == request.PropertyType.Value);
        if (request.MinPrice.HasValue)
            query = query.Where(l => l.Price.Amount >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue)
            query = query.Where(l => l.Price.Amount <= request.MaxPrice.Value);
        if (request.MinBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= request.MinBedrooms.Value);

        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var projected = query
            .OrderByDescending(l => l.Created)
            .ProjectToBrief();

        return await PaginatedList<ListingBriefDto>
            .CreateAsync(projected, pageNumber, pageSize, cancellationToken);
    }
}