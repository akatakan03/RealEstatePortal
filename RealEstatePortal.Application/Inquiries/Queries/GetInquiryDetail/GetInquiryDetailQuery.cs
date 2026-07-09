using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Inquiries.Queries;

namespace RealEstatePortal.Application.Inquiries.Queries.GetInquiryDetail;

public record GetInquiryDetailQuery(int Id) : IRequest<InquiryDto?>;

public class GetInquiryDetailQueryHandler : IRequestHandler<GetInquiryDetailQuery, InquiryDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetInquiryDetailQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<InquiryDto?> Handle(GetInquiryDetailQuery request, CancellationToken cancellationToken)
    {
        var query =
            from i in _context.Inquiries
            join l in _context.Listings on i.ListingId equals l.Id
            where i.Id == request.Id && l.OwnerId == _user.Id
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

        return await query.FirstOrDefaultAsync(cancellationToken);
    }
}