using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using RealEstatePortal.Web.ModelBinding;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

/// Guards a bug that shipped invisibly once the site started serving Turkish.
///
/// The map picker writes "40.990000" into a hidden input — JavaScript's toFixed is dot-decimal
/// in every locale. MVC binds form values with the request culture, and under tr-TR the dot is a
/// group separator, so that string bound as 40990000, the latitude validator rejected it, and
/// because the input is hidden the agent saw a form that silently refused to submit.
///
/// These tests need no database, so they stay outside the fixture collections.
public class InvariantDoubleModelBinderTests
{
    private static async Task<ModelBindingResult> BindAsync(string? value, System.Type modelType)
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType);
        var context = new DefaultModelBindingContext
        {
            ModelName = "Latitude",
            ModelMetadata = metadata,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new SimpleValueProvider(value)
        };

        await new InvariantDoubleModelBinder().BindModelAsync(context);
        return context.Result;
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]   // comma decimal AND dot grouping, like Turkish
    public async Task DotDecimalBindsIdentically_WhateverTheRequestCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var result = await BindAsync("40.990000", typeof(double?));

            result.IsModelSet.ShouldBeTrue();
            ((double?)result.Model)!.Value.ShouldBe(40.99, 0.0000001);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task EmptyValueBindsAsNull_SoAListingWithoutAPinStillSaves()
    {
        var result = await BindAsync("", typeof(double?));

        result.IsModelSet.ShouldBeTrue();
        result.Model.ShouldBeNull();
    }

    [Fact]
    public async Task GarbageIsRejectedRatherThanSilentlyBecomingZero()
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(double?));
        var context = new DefaultModelBindingContext
        {
            ModelName = "Latitude",
            ModelMetadata = metadata,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new SimpleValueProvider("not a number")
        };

        await new InvariantDoubleModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.ShouldBeFalse();
        context.ModelState["Latitude"]!.Errors.ShouldNotBeEmpty();
    }

    private sealed class SimpleValueProvider : IValueProvider
    {
        private readonly string? _value;
        public SimpleValueProvider(string? value) => _value = value;

        public bool ContainsPrefix(string prefix) => true;

        public ValueProviderResult GetValue(string key) =>
            _value is null
                ? ValueProviderResult.None
                : new ValueProviderResult(new StringValues(_value), CultureInfo.InvariantCulture);
    }
}
