using Microsoft.AspNetCore.Mvc.ModelBinding;
using RealEstatePortal.Application.Common.Exceptions;

namespace RealEstatePortal.Web.Helpers;

public static class ModelStateExtensions
{
    /// Copies a failed validation onto the model state so the form redisplays with each message
    /// against the field it belongs to.
    ///
    /// <param name="prefix">
    /// Set when the command is bound under a property of the page's view model rather than being
    /// the model itself — the keys have to match what the tag helpers rendered, or the messages
    /// render as a summary at the top instead of next to their inputs.
    /// </param>
    public static void AddValidationErrors(
        this ModelStateDictionary modelState, ValidationException exception, string? prefix = null)
    {
        foreach (var (property, errors) in exception.Errors)
            foreach (var error in errors)
                modelState.AddModelError(prefix is null ? property : $"{prefix}.{property}", error);
    }
}
