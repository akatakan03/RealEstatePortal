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
    private const int RecentDays = 7;

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IIdentityService _identity; // 1. YENİ: Identity servisi eklendi
    private readonly TimeProvider _clock;

    public GetListingDetailQueryHandler(
        IApplicationDbContext context,
        IFileStorageService storage,
        IIdentityService identity, // 1. YENİ: Constructor'a enjekte edildi
        TimeProvider clock)
    {
        _context = context;
        _storage = storage;
        _identity = identity; // 1. YENİ: Field ataması yapıldı
        _clock = clock;
    }

    public async Task<ListingDetailDto?> Handle(
        GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        // Only Media is included here. Pulling the price history in the same statement would
        // make the database answer with every (photo × price point) pairing — up to 20 photos
        // times however many price changes — repeating the 4000-character description in each
        // of those rows. It is fetched separately below instead.
        var query = _context.Listings
            .Include(l => l.Media)
            .Where(l => l.Id == request.Id);

        // The public site sees only Active listings; a privileged caller can preview any status.
        if (!request.IncludeNonPublic)
            query = query.Where(l => l.Status == ListingStatus.Active);

        var entity = await query.FirstOrDefaultAsync(cancellationToken);

        if (entity is null) return null;

        var dto = ListingMapper.ToDetail(entity);

        // Its own statement, projected to the three columns the chart actually plots — lighter
        // than materialising the entities, and it keeps the listing row out of the result.
        dto.PriceHistory = await _context.Listings
            .Where(l => l.Id == entity.Id)
            .SelectMany(l => l.PriceHistory)
            .OrderBy(p => p.ChangedAt)
            .Select(p => new PricePointDto(p.Amount, p.Currency, p.ChangedAt))
            .ToListAsync(cancellationToken);
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

        // Interest signals. Counted the same calendar-day-aligned way as the agent dashboard,
        // so a buyer and the agent who owns the listing are always looking at the same number.
        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var since = new DateTimeOffset(
            today.AddDays(-(RecentDays - 1)).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Both counts in one round trip. This is the busiest public page, so a second wait for
        // a badge isn't worth it — and each count is an index seek
        // (ListingViews(ListingId, ViewedAt) and the Favorites FK index on ListingId), never a scan.
        var signals = await _context.Listings
            .Where(l => l.Id == entity.Id)
            .Select(l => new
            {
                Views7d = _context.ListingViews.Count(v => v.ListingId == l.Id && v.ViewedAt >= since),
                Saves = _context.Favorites.Count(f => f.ListingId == l.Id)
            })
            .FirstAsync(cancellationToken);

        dto.Views7d = signals.Views7d;
        dto.SaveCount = signals.Saves;

        // Free: the listing row is already loaded, so this costs no query at all.
        dto.IsNew = entity.Created >= now.AddDays(-RecentDays);

        return dto;
    }
}