using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.SavedSearches.Queries.GetMySavedSearches;

public record SavedSearchDto(
    int Id, string Name, string? Keyword,
    ListingType? ListingType, PropertyType? PropertyType,
    decimal? MaxPrice, int? MinBedrooms);

public record GetMySavedSearchesQuery : IRequest<List<SavedSearchDto>>;

public class GetMySavedSearchesQueryHandler
    : IRequestHandler<GetMySavedSearchesQuery, List<SavedSearchDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetMySavedSearchesQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<SavedSearchDto>> Handle(
        GetMySavedSearchesQuery request, CancellationToken cancellationToken)
        => await _context.SavedSearches
            .Where(s => s.UserId == _user.Id)
            .OrderByDescending(s => s.Created)
            .Select(s => new SavedSearchDto(
                s.Id, s.Name, s.Keyword, s.ListingType, s.PropertyType, s.MaxPrice, s.MinBedrooms))
            .ToListAsync(cancellationToken);
}