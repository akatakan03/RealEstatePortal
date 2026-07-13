using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RealEstatePortal.Application.Common.Exceptions;

namespace RealEstatePortal.Web.Filters;

public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case NotFoundException:
                context.Result = new NotFoundResult();          // 404
                context.ExceptionHandled = true;
                break;

            case ForbiddenAccessException:
                context.Result = new ForbidResult();            // 403 -> AccessDenied page
                context.ExceptionHandled = true;
                break;

                // ValidationException is intentionally NOT handled here — form actions
                // catch it themselves to redisplay the form with field errors.
        }
    }
}