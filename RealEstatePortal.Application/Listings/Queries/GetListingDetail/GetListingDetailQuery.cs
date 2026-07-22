using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models; // Gerekirse ekleyin (AgentProfile vb. modeller için)
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingDetail;

// IncludeNonPublic lets a privileged caller (an admin, or the listing's owner) preview a
// listing that isn't Active yet. Left false for the public site, which only sees Active listings.
public record GetListingDetailQuery(int Id, bool IncludeNonPublic = false) : IRequest<ListingDetailDto?>;

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
        var query = _context.Listings
            .Include(l => l.Media)
            .Include(l => l.PriceHistory)
            .Where(l => l.Id == request.Id);

        // The public site sees only Active listings; a privileged caller can preview any status.
        if (!request.IncludeNonPublic)
            query = query.Where(l => l.Status == ListingStatus.Active);

        var entity = await query.FirstOrDefaultAsync(cancellationToken);

        if (entity is null) return null;

        var dto = ListingMapper.ToDetail(entity);

        dto.PriceHistory = entity.PriceHistory
            .OrderBy(p => p.ChangedAt)
            .Select(p => new PricePointDto(p.Amount, p.Currency, p.ChangedAt))
            .ToList();
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