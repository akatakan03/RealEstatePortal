using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Inquiries.Queries.GetUnreadInquiryCount;
using RealEstatePortal.Domain.Constants;

namespace RealEstatePortal.Web.ViewComponents;

public class UnreadInquiriesViewComponent : ViewComponent
{
    private readonly ISender _sender;

    public UnreadInquiriesViewComponent(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.IsInRole(Roles.Agent))
            return Content(string.Empty);

        var count = await _sender.Send(new GetUnreadInquiryCountQuery());
        return View(count);
    }
}