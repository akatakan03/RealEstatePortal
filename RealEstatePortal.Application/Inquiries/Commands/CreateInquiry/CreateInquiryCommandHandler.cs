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
    private readonly ILocalizedText _text;
    private readonly ILogger<CreateInquiryCommandHandler> _logger;

    public CreateInquiryCommandHandler(
        IApplicationDbContext context,
        IEmailService email,
        IIdentityService identity,
        ILocalizedText text,
        ILogger<CreateInquiryCommandHandler> logger)
    {
        _context = context;
        _email = email;
        _identity = identity;
        _text = text;
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
                var recipient = await _identity.GetEmailRecipientAsync(listing.OwnerId, cancellationToken);
                if (recipient is not null)
                {
                    // Written in the agent's language, not the visitor's: the visitor may well be
                    // browsing in English while the agent reads Turkish, and this is the agent's mail.
                    var culture = recipient.Culture;

                    // The title was going into the body unencoded — an apostrophe or angle bracket
                    // in an agent's own title would have landed as markup.
                    var title = $"<strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong>";
                    var from = System.Net.WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(request.Phone)
                            ? $"{request.Name} ({request.Email})"
                            : $"{request.Name} ({request.Email}, {request.Phone})");

                    var subject = _text.For(culture, "New inquiry for \"{0}\"", listing.Title);
                    var body =
                        "<p>" + _text.For(culture,
                            "You received a new inquiry on your listing {0}.", title) + "</p>" +
                        "<p><strong>" + _text.For(culture, "From:") + "</strong> " + from + "</p>" +
                        "<p><strong>" + _text.For(culture, "Message:") + "</strong><br/>" +
                        System.Net.WebUtility.HtmlEncode(request.Message) + "</p>";

                    await _email.SendAsync(recipient.Email, subject, body, cancellationToken);
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