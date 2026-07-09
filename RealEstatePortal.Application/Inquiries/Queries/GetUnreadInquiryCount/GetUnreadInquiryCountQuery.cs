using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Inquiries.Queries.GetUnreadInquiryCount;

public record GetUnreadInquiryCountQuery : IRequest<int>;

public class GetUnreadInquiryCountQueryHandler : IRequestHandler<GetUnreadInquiryCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetUnreadInquiryCountQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<int> Handle(GetUnreadInquiryCountQuery request, CancellationToken cancellationToken)
    {
        return await (
            from i in _context.Inquiries
            join l in _context.Listings on i.ListingId equals l.Id
            where l.OwnerId == _user.Id && i.Status == InquiryStatus.New
            select i.Id
        ).CountAsync(cancellationToken);
    }
}