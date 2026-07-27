using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingsForCompare;

// Fetches the handful of listings a buyer picked to compare. Only public (Active) listings come
// back, capped at MaxItems, in the order they were requested so the columns match the order the
// buyer clicked them. Ids that don't resolve to an active listing are silently dropped.
public record GetListingsForCompareQuery(IReadOnlyList<int> Ids) : IRequest<IReadOnlyList<CompareListingDto>>;

public class GetListingsForCompareQueryHandler
    : IRequestHandler<GetListingsForCompareQuery, IReadOnlyList<CompareListingDto>>
{
    // A side-by-side table stops being readable past four columns on a phone, and the compare
    // bar enforces the same ceiling — this is the server's copy of that rule.
    public const int MaxItems = 4;

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    public GetListingsForCompareQueryHandler(IApplicationDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<IReadOnlyList<CompareListingDto>> Handle(
        GetListingsForCompareQuery request, CancellationToken cancellationToken)
    {
        // Distinct, in first-seen order, then capped. Doing this before the query keeps a padded
        // or duplicate-laden URL from turning into a large IN (...) or a wide table.
        var ids = request.Ids.Distinct().Take(MaxItems).ToList();
        if (ids.Count == 0)
            return Array.Empty<CompareListingDto>();

        var found = await _context.Listings
            .Where(l => l.Status == ListingStatus.Active && ids.Contains(l.Id))
            .Select(l => new CompareListingDto
            {
                Id = l.Id,
                Title = l.Title,
                Slug = l.Slug,
                Address = l.Address,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                ListingType = l.ListingType,
                PropertyType = l.PropertyType,
                Bedrooms = l.Bedrooms,
                Bathrooms = l.Bathrooms,
                AreaSqMeters = l.AreaSqMeters,
                Heating = l.Heating,
                Internet = l.Internet,
                IsFurnished = l.IsFurnished,
                HasBalcony = l.HasBalcony,
                HasParking = l.HasParking,
                FloorNumber = l.FloorNumber,
                TotalFloors = l.TotalFloors,
                BuildingAge = l.BuildingAge,
                MonthlyDues = l.MonthlyDues,
                CoverThumbnailKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var item in found)
            item.CoverThumbnailUrl = item.CoverThumbnailKey is null
                ? null
                : _storage.GetPublicUrl(item.CoverThumbnailKey);

        // SQL returns rows in whatever order it likes; restore the buyer's click order so the
        // columns line up with what they selected.
        var byId = found.ToDictionary(l => l.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }
}
