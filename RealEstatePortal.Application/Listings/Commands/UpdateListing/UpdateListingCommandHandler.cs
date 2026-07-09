using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Application.Listings.Commands.UpdateListing;

public class UpdateListingCommandHandler : IRequestHandler<UpdateListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpdateListingCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(UpdateListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        if (entity.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Price = new Money(request.Price, request.Currency);
        entity.ListingType = request.ListingType;
        entity.PropertyType = request.PropertyType;
        entity.Bedrooms = request.Bedrooms;
        entity.Bathrooms = request.Bathrooms;
        entity.AreaSqMeters = request.AreaSqMeters;
        entity.Address = request.Address;
        // Slug intentionally left unchanged — stable URLs are better for SEO.

        await _context.SaveChangesAsync(cancellationToken);
    }
}