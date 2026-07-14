using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models; // Gerekirse ekleyin (AgentProfile vb. modeller için)
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingDetail;

public record GetListingDetailQuery(int Id) : IRequest<ListingDetailDto?>;

public class GetListingDetailQueryHandler
    : IRequestHandler<GetListingDetailQuery, ListingDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IIdentityService _identity; // 1. YENİ: Identity servisi eklendi

    public GetListingDetailQueryHandler(
        IApplicationDbContext context,
        IFileStorageService storage,
        IIdentityService identity) // 1. YENİ: Constructor'a enjekte edildi
    {
        _context = context;
        _storage = storage;
        _identity = identity; // 1. YENİ: Field ataması yapıldı
    }

    public async Task<ListingDetailDto?> Handle(
        GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(
                l => l.Id == request.Id && l.Status == ListingStatus.Active,
                cancellationToken);

        if (entity is null) return null;

        var dto = ListingMapper.ToDetail(entity);
        if (entity.Location is not null)
        {
            dto.Latitude = entity.Location.Latitude;
            dto.Longitude = entity.Location.Longitude;
        }
        dto.ImageUrls = entity.Media
            .OrderByDescending(m => m.IsCover)   // önce kapak resmi
            .ThenBy(m => m.Order)
            .Select(m => _storage.GetPublicUrl(m.ObjectKey))
            .ToList();

        // 2. YENİ: İlan sahibinin (Agent) bilgilerini çekme ve DTO'ya ekleme adımı
        dto.OwnerId = entity.OwnerId ?? string.Empty;

        var owner = entity.OwnerId is null
            ? null
            : await _identity.GetAgentProfileAsync(entity.OwnerId, cancellationToken);

        if (owner is not null)
        {
            // Kullanıcının DisplayName'i yoksa e-postasını kullan, varsa DisplayName'i yaz
            dto.OwnerName = string.IsNullOrWhiteSpace(owner.DisplayName) ? owner.Email : owner.DisplayName;

            // Eğer bir avatar görseli (AvatarKey) varsa public URL'ini oluştur, yoksa null bırak
            dto.OwnerAvatarUrl = owner.AvatarKey is null ? null : _storage.GetPublicUrl(owner.AvatarKey);
        }

        return dto;
    }
}