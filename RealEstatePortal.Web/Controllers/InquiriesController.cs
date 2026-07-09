using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Inquiries.Commands.MarkInquiryHandled;
using RealEstatePortal.Application.Inquiries.Commands.MarkInquiryRead;
using RealEstatePortal.Application.Inquiries.Queries.GetInquiryDetail;
using RealEstatePortal.Application.Inquiries.Queries.GetMyInquiries;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;
using ForbiddenAccessException = RealEstatePortal.Application.Common.Exceptions.ForbiddenAccessException;
using NotFoundException = RealEstatePortal.Application.Common.Exceptions.NotFoundException;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class InquiriesController : Controller
{
    private readonly ISender _sender;

    public InquiriesController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index()
    {
        var inquiries = await _sender.Send(new GetMyInquiriesQuery());
        return View(inquiries);
    }

    public async Task<IActionResult> Details(int id)
    {
        var inquiry = await _sender.Send(new GetInquiryDetailQuery(id));
        if (inquiry is null) return NotFound();

        // Opening a new inquiry marks it read (command mutates; query stays pure).
        if (inquiry.Status == InquiryStatus.New)
        {
            await _sender.Send(new MarkInquiryReadCommand(id));
            inquiry = await _sender.Send(new GetInquiryDetailQuery(id));
            if (inquiry is null) return NotFound();
        }

        return View(inquiry);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkHandled(int id)
    {
        try
        {
            await _sender.Send(new MarkInquiryHandledCommand(id));
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ForbiddenAccessException) { return Forbid(); }

        return RedirectToAction(nameof(Details), new { id });
    }
}