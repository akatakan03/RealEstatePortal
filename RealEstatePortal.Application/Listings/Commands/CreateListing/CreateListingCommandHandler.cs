using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Utilities;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Application.Listings.Commands.CreateListing;

public class CreateListingCommandHandler : IRequestHandler<CreateListingCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateListingCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateListingCommand request, CancellationToken cancellationToken)
    {
        // Ensure the slug is unique (we have a unique index on Slug)
        var baseSlug = SlugGenerator.Generate(request.Title);
        var slug = baseSlug;
        var suffix = 2;
        while (await _context.Listings.AnyAsync(l => l.Slug == slug, cancellationToken))
            slug = $"{baseSlug}-{suffix++}";

        var entity = new Listing
        {
            Title = request.Title,
            Slug = slug,
            Description = request.Description,
            Price = new Money(request.Price, request.Currency),
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AreaSqMeters = request.AreaSqMeters,
            Address = request.Address
        };

        _context.Listings.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}