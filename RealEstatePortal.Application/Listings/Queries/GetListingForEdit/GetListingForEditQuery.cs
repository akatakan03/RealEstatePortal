using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;

namespace RealEstatePortal.Application.Listings.Queries.GetListingForEdit;

public record GetListingForEditQuery(int Id) : IRequest<UpdateListingCommand?>;

public class GetListingForEditQueryHandler
    : IRequestHandler<GetListingForEditQuery, UpdateListingCommand?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetListingForEditQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<UpdateListingCommand?> Handle(
        GetListingForEditQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        // Not found, or not owned by the current agent -> treat identically (null)
        if (entity is null || entity.OwnerId != _user.Id)
            return null;

        return new UpdateListingCommand
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Price = entity.Price.Amount,
            Currency = entity.Price.Currency,
            ListingType = entity.ListingType,
            PropertyType = entity.PropertyType,
            Bedrooms = entity.Bedrooms,
            Bathrooms = entity.Bathrooms,
            AreaSqMeters = entity.AreaSqMeters,
            Address = entity.Address
        };
    }
}