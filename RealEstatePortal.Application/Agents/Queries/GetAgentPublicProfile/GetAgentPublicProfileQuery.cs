using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetAgentPublicProfile;

public record AgentPublicProfileDto(
    string UserId,
    string DisplayName,
    string Email,
    string? Bio,
    string? AvatarUrl,
    List<ListingBriefDto> Listings);

public record GetAgentPublicProfileQuery(string AgentId) : IRequest<AgentPublicProfileDto?>;

public class GetAgentPublicProfileQueryHandler
    : IRequestHandler<GetAgentPublicProfileQuery, AgentPublicProfileDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identity;
    private readonly IFileStorageService _storage;

    public GetAgentPublicProfileQueryHandler(
        IApplicationDbContext context, IIdentityService identity, IFileStorageService storage)
    {
        _context = context;
        _identity = identity;
        _storage = storage;
    }

    public async Task<AgentPublicProfileDto?> Handle(
        GetAgentPublicProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _identity.GetAgentProfileAsync(request.AgentId, cancellationToken);
        if (profile is null) return null;   // not found, or not an agent

        var listings = await (
            from l in _context.Listings
            where l.OwnerId == request.AgentId && l.Status == ListingStatus.Active
            orderby l.Created descending
            select new ListingBriefDto
            {
                Id = l.Id,
                Title = l.Title,
                Slug = l.Slug,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                ListingType = l.ListingType,
                PropertyType = l.PropertyType,
                Status = l.Status,
                Bedrooms = l.Bedrooms,
                AreaSqMeters = l.AreaSqMeters,
                CoverThumbnailKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            }).ToListAsync(cancellationToken);

        foreach (var item in listings)
            item.CoverThumbnailUrl = item.CoverThumbnailKey is null ? null : _storage.GetPublicUrl(item.CoverThumbnailKey);

        return new AgentPublicProfileDto(
            profile.UserId,
            string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Email : profile.DisplayName!,
            profile.Email,
            profile.Bio,
            profile.AvatarKey is null ? null : _storage.GetPublicUrl(profile.AvatarKey),
            listings);
    }
}