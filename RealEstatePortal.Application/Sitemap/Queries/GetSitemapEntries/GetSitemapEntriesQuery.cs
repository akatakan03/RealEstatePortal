using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Sitemap.Queries.GetSitemapEntries;

public record GetSitemapEntriesQuery : IRequest<List<SitemapEntryDto>>;

public class GetSitemapEntriesQueryHandler
    : IRequestHandler<GetSitemapEntriesQuery, List<SitemapEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSitemapEntriesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SitemapEntryDto>> Handle(
        GetSitemapEntriesQuery request, CancellationToken cancellationToken)
    {
        // Only active (public) listings belong in the sitemap.
        return await _context.Listings
            .Where(l => l.Status == ListingStatus.Active)
            .OrderByDescending(l => l.LastModified)
            .Select(l => new SitemapEntryDto
            {
                Id = l.Id,
                Slug = l.Slug,
                LastModified = l.LastModified
            })
            .ToListAsync(cancellationToken);
    }
}