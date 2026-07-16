using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;

public class CreateInquiryCommandHandler : IRequestHandler<CreateInquiryCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _email;
    private readonly IIdentityService _identity;
    private readonly ILogger<CreateInquiryCommandHandler> _logger;

    public CreateInquiryCommandHandler(
        IApplicationDbContext context,
        IEmailService email,
        IIdentityService identity,
        ILogger<CreateInquiryCommandHandler> logger)
    {
        _context = context;
        _email = email;
        _identity = identity;
        _logger = logger;
    }

    public async Task<int> Handle(CreateInquiryCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(
                l => l.Id == request.ListingId && l.Status == ListingStatus.Active,
                cancellationToken);

        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.ListingId);

        var inquiry = Inquiry.Create(
            listing.Id, request.Name, request.Email, request.Phone, request.Message);

        _context.Inquiries.Add(inquiry);
        await _context.SaveChangesAsync(cancellationToken);   // save FIRST — never lose the lead

        // Notify the agent. A mail failure must not fail the request or lose the inquiry.
        try
        {
            if (listing.OwnerId is not null)
            {
                var agentEmail = await _identity.GetUserEmailAsync(listing.OwnerId, cancellationToken);
                if (!string.IsNullOrEmpty(agentEmail))
                {
                    var subject = $"New inquiry for \"{listing.Title}\"";
                    var body =
                        $"<p>You received a new inquiry on your listing <strong>{listing.Title}</strong>.</p>" +
                        $"<p><strong>From:</strong> {request.Name} ({request.Email}" +
                        (string.IsNullOrWhiteSpace(request.Phone) ? "" : $", {request.Phone}") + ")</p>" +
                        $"<p><strong>Message:</strong><br/>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>";

                    await _email.SendAsync(agentEmail, subject, body, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send inquiry notification email for inquiry {InquiryId}", inquiry.Id);
        }

        return inquiry.Id;
    }
}