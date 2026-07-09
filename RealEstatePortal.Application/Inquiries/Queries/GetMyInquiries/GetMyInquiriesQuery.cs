using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Inquiries.Queries.GetMyInquiries;

public record GetMyInquiriesQuery : IRequest<List<InquiryDto>>;

public class GetMyInquiriesQueryHandler : IRequestHandler<GetMyInquiriesQuery, List<InquiryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetMyInquiriesQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<InquiryDto>> Handle(GetMyInquiriesQuery request, CancellationToken cancellationToken)
    {
        // Join inquiry -> listing, scope to listings owned by the current agent.
        var query =
            from i in _context.Inquiries
            join l in _context.Listings on i.ListingId equals l.Id
            where l.OwnerId == _user.Id
            orderby i.Created descending
            select new InquiryDto
            {
                Id = i.Id,
                ListingId = i.ListingId,
                ListingTitle = l.Title,
                Name = i.Name,
                Email = i.Email,
                Phone = i.Phone,
                Message = i.Message,
                Status = i.Status,
                Created = i.Created
            };

        return await query.ToListAsync(cancellationToken);
    }
}