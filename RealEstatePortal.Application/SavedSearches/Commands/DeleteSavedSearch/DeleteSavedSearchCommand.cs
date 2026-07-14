using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.SavedSearches.Commands.DeleteSavedSearch;

public record DeleteSavedSearchCommand(int Id) : IRequest;

public class DeleteSavedSearchCommandHandler : IRequestHandler<DeleteSavedSearchCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteSavedSearchCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DeleteSavedSearchCommand request, CancellationToken cancellationToken)
    {
        var search = await _context.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (search is null)
            throw new NotFoundException(nameof(SavedSearch), request.Id);

        // Ownership: you can only delete your own saved search.
        if (search.UserId != _user.Id)
            throw new ForbiddenAccessException();

        _context.SavedSearches.Remove(search);
        await _context.SaveChangesAsync(cancellationToken);
    }
}