using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace RealEstatePortal.Web.ModelBinding;

/// Binds double and double? from the invariant culture instead of the request culture.
///
/// MVC parses form values with the current culture, which is right for a number a person typed:
/// a Turkish visitor writing a price as 1.500.000 means one and a half million. It is wrong for a
/// number a machine wrote. The only doubles posted by this application are the latitude and
/// longitude the map picker writes into hidden inputs as "40.990000" — always dot-decimal, because
/// that is what JavaScript's toFixed produces regardless of locale.
///
/// Read under tr-TR the dot is a group separator, so "40.990000" binds as 40990000, the validator
/// rejects it as out of range, and because those fields are hidden the agent sees a form that
/// simply refuses to submit with nothing marked wrong. That is the failure this prevents.
///
/// Deliberately scoped to double. decimal stays on the request culture, which is what the price
/// and area fields need — those are typed by hand.
public class InvariantDoubleModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

        var raw = value.FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            // An empty box means "no coordinate", which is valid for a nullable. For a
            // non-nullable double, leaving the result unset lets MVC report it as required.
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            bindingContext.Result = ModelBindingResult.Success(parsed);
        else
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                bindingContext.ModelMetadata.ModelBindingMessageProvider
                    .ValueIsInvalidAccessor(raw));

        return Task.CompletedTask;
    }
}

public class InvariantDoubleModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type == typeof(double) ? new InvariantDoubleModelBinder() : null;
    }
}
