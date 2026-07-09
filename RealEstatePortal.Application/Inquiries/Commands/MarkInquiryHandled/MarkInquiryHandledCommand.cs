using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Inquiries.Commands.MarkInquiryHandled;

public record MarkInquiryHandledCommand(int Id) : IRequest;

public class MarkInquiryHandledCommandHandler : IRequestHandler<MarkInquiryHandledCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public MarkInquiryHandledCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(MarkInquiryHandledCommand request, CancellationToken cancellationToken)
    {
        var inquiry = await _context.Inquiries
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (inquiry is null)
            throw new NotFoundException(nameof(Inquiry), request.Id);

        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == inquiry.ListingId, cancellationToken);
        if (listing is null || listing.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        inquiry.MarkAsHandled();
        await _context.SaveChangesAsync(cancellationToken);
    }
}