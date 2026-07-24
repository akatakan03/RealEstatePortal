using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace RealEstatePortal.Web.ModelBinding;

/// Replaces the framework's "The field {0} must be a number." client-side message, which is baked
/// into a framework resource with no hook to translate it.
///
/// The field name is dropped rather than translated. It came out as the property name — a Turkish
/// agent was told "The field AreaSqMeters must be a number." — because these commands live in the
/// Application layer and carry no [Display] attributes; their labels are written in the view. The
/// message renders directly beneath the box it belongs to, so naming the field adds nothing.
public class LocalizedNumericClientModelValidator : IClientModelValidator
{
    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Resolved per call rather than injected: the provider is registered once at startup,
        // while the localizer has to answer for the culture of the request being rendered.
        var localizer = context.ActionContext.HttpContext.RequestServices
            .GetRequiredService<IStringLocalizer<SharedResource>>();

        SetIfAbsent(context, "data-val", "true");
        SetIfAbsent(context, "data-val-number", localizer["Please enter a valid number."].Value);
    }

    private static void SetIfAbsent(ClientModelValidationContext context, string key, string value)
    {
        if (!context.Attributes.ContainsKey(key))
            context.Attributes.Add(key, value);
    }
}

/// Registered ahead of the framework's own numeric provider, whose type is internal and so cannot
/// be replaced by name. It does not need to be: the framework writes data-val-number with a merge
/// that leaves an existing attribute alone, so whichever validator runs first wins. Registering
/// this one at the front of the list is what makes that us.
///
/// The type check mirrors the framework's: only the floating-point types carry a client-side
/// "is this a number" rule, because those are the ones whose text form varies by culture.
public class LocalizedNumericClientModelValidatorProvider : IClientModelValidatorProvider
{
    public void CreateValidators(ClientValidatorProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.ModelMetadata.UnderlyingOrModelType;
        if (type != typeof(float) && type != typeof(double) && type != typeof(decimal))
            return;

        foreach (var result in context.Results)
        {
            if (result.Validator is LocalizedNumericClientModelValidator)
                return;
        }

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new LocalizedNumericClientModelValidator(),
            IsReusable = true
        });
    }
}
