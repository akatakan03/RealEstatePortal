using MediatR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.SavedSearches.Commands.CreateSavedSearch;

public record CreateSavedSearchCommand : IRequest<int>
{
    public string Name { get; init; } = string.Empty;
    public string? Keyword { get; init; }
    public ListingType? ListingType { get; init; }
    public PropertyType? PropertyType { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBedrooms { get; init; }
}

public class CreateSavedSearchCommandHandler : IRequestHandler<CreateSavedSearchCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreateSavedSearchCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<int> Handle(CreateSavedSearchCommand request, CancellationToken cancellationToken)
    {
        var search = new SavedSearch
        {
            UserId = _user.Id!,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "My search" : request.Name.Trim(),
            Keyword = request.Keyword,
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            MaxPrice = request.MaxPrice,
            MinBedrooms = request.MinBedrooms
        };

        _context.SavedSearches.Add(search);
        await _context.SaveChangesAsync(cancellationToken);
        return search.Id;
    }
}