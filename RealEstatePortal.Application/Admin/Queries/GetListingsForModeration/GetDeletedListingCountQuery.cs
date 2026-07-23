using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

// How many deleted listings are still restorable — the number on the moderation page's trash
// tab. Served by the filtered DeletedAt index, so it costs nothing on a page that already
// runs a handful of queries.
public record GetDeletedListingCountQuery : IRequest<int>;

public class GetDeletedListingCountQueryHandler : IRequestHandler<GetDeletedListingCountQuery, int>
{
    private readonly IApplicationDbContext _context;

    public GetDeletedListingCountQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<int> Handle(GetDeletedListingCountQuery request, CancellationToken cancellationToken) =>
        _context.Listings
            .IgnoreQueryFilters()
            .CountAsync(l => l.DeletedAt != null, cancellationToken);
}
