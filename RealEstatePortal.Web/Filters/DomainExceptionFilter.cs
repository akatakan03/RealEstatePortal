using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RealEstatePortal.Application.Common.Exceptions;

namespace RealEstatePortal.Web.Filters;

public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var isApi = context.HttpContext.Request.Path.StartsWithSegments("/api");

        switch (context.Exception)
        {
            // API validation failures -> 400 with a problem-details body.
            // (MVC form actions catch ValidationException themselves, so this only fires for /api.)
            case ValidationException validationEx when isApi:
                context.Result = new BadRequestObjectResult(
                    new ValidationProblemDetails(validationEx.Errors));
                context.ExceptionHandled = true;
                break;

            // Domain rule violations (e.g. publishing a locked listing) -> 409 Conflict.
            case ArgumentException domainEx:
                context.Result = isApi
                    ? new ConflictObjectResult(new ProblemDetails
                    {
                        Title = "Operation not allowed",
                        Detail = domainEx.Message,
                        Status = StatusCodes.Status409Conflict
                    })
                    : new StatusCodeResult(StatusCodes.Status409Conflict);
                context.ExceptionHandled = true;
                break;

            case NotFoundException:
                context.Result = new NotFoundResult();          // 404 for both API and MVC
                context.ExceptionHandled = true;
                break;

            case ForbiddenAccessException:
                // API -> hard 403; MVC -> cookie Forbid (redirects to AccessDenied page).
                context.Result = isApi
                    ? new StatusCodeResult(StatusCodes.Status403Forbidden)
                    : new ForbidResult();
                context.ExceptionHandled = true;
                break;
        }
    }
}