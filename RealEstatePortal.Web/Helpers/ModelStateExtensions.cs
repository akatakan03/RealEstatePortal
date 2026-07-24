using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using RealEstatePortal.Application.Common.Exceptions;

namespace RealEstatePortal.Web.Helpers;

public static class ModelStateExtensions
{
    /// Copies a failed validation onto the model state so the form redisplays with each message
    /// against the field it belongs to, translating it on the way.
    ///
    /// The validators live in the Application layer and write their messages in English, which is
    /// exactly how this resource file is keyed — so the English text doubles as the lookup and the
    /// Application layer needs no idea that the site speaks two languages.
    ///
    /// FluentValidation's own built-in messages ("must not be empty") arrive already translated:
    /// the library ships those and picks the language from the current UI culture. They have no
    /// entry here, so the localizer hands them straight back untouched.
    ///
    /// <param name="prefix">
    /// Set when the command is bound under a property of the page's view model rather than being
    /// the model itself — the keys have to match what the tag helpers rendered, or the messages
    /// render as a summary at the top instead of next to their inputs.
    /// </param>
    public static void AddValidationErrors(
        this ModelStateDictionary modelState,
        ValidationException exception,
        IStringLocalizer localizer,
        string? prefix = null)
    {
        foreach (var (property, errors) in exception.Errors)
            foreach (var error in errors)
                modelState.AddModelError(
                    prefix is null ? property : $"{prefix}.{property}",
                    localizer[error]);
    }
}
