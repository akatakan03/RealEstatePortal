using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Search;

public class GeminiNaturalLanguageSearchParserTests
{
    // Replays a fixed Gemini response and records the request, so a test can assert on both.
    private class GeminiStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }

        public GeminiStubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }

    // Wraps a filter object as Gemini would: the structured JSON is a string inside the first part.
    private static string GeminiEnvelope(string filterJson)
    {
        // JsonSerializer turns the filter JSON into a quoted, escaped string literal — exactly how
        // Gemini nests the structured output inside the first content part.
        var escaped = System.Text.Json.JsonSerializer.Serialize(filterJson);
        return "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":" + escaped + "}]}}]}";
    }

    private static GeminiNaturalLanguageSearchParser Build(
        GeminiStubHandler handler, string? apiKey = "test-key")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gemini.test/v1beta/")
        };
        var settings = Options.Create(new GeminiSettings { ApiKey = apiKey, Model = "gemini-2.0-flash" });
        return new GeminiNaturalLanguageSearchParser(
            client, settings, new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILogger<GeminiNaturalLanguageSearchParser>>());
    }

    [Fact]
    public async Task ParseAsync_MapsFieldsAndEnums()
    {
        var filter = """
            {"listingType":"Rent","propertyType":"Apartment","minBedrooms":3,
             "maxPrice":45000,"heating":"NaturalGas","balcony":true,"furnished":true,
             "locationText":"Kadıköy","unmatchedCriteria":["metroya yakın"]}
            """;
        var handler = new GeminiStubHandler(GeminiEnvelope(filter));

        var result = await Build(handler).ParseAsync("Kadıköy'de balkonlu, eşyalı 3+1 kiralık daire");

        result.ShouldNotBeNull();
        result!.ListingType.ShouldBe(ListingType.Rent);
        result.PropertyType.ShouldBe(PropertyType.Apartment);
        result.MinBedrooms.ShouldBe(3);
        result.MaxPrice.ShouldBe(45000);
        result.Heating.ShouldBe(HeatingType.NaturalGas);
        result.Balcony.ShouldBe(true);
        result.Furnished.ShouldBe(true);
        result.LocationText.ShouldBe("Kadıköy");
        result.UnmatchedCriteria.ShouldContain("metroya yakın");
    }

    [Fact]
    public async Task ParseAsync_SendsKeyAndModelInUrl()
    {
        var handler = new GeminiStubHandler(GeminiEnvelope("{}"));

        await Build(handler, apiKey: "secret-123").ParseAsync("herhangi bir şey");

        handler.LastUrl.ShouldNotBeNull();
        handler.LastUrl!.ShouldContain("models/gemini-2.0-flash:generateContent");
        handler.LastUrl!.ShouldContain("key=secret-123");
    }

    [Fact]
    public async Task ParseAsync_ReturnsNull_WhenNoApiKey()
    {
        var handler = new GeminiStubHandler(GeminiEnvelope("{}"));

        var result = await Build(handler, apiKey: null).ParseAsync("Kadıköy daire");

        result.ShouldBeNull();
        handler.Calls.ShouldBe(0); // never touches the network without a key
    }

    [Fact]
    public async Task ParseAsync_ReturnsNull_OnHttpError()
    {
        var handler = new GeminiStubHandler("nope", HttpStatusCode.TooManyRequests);

        var result = await Build(handler).ParseAsync("Kadıköy daire");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_DropsUnknownEnumValues()
    {
        // A value outside our enum must be ignored, not blow up the whole parse.
        var handler = new GeminiStubHandler(GeminiEnvelope("""{"heating":"Geothermal","balcony":true}"""));

        var result = await Build(handler).ParseAsync("jeotermal ısıtmalı");

        result.ShouldNotBeNull();
        result!.Heating.ShouldBeNull();
        result.Balcony.ShouldBe(true);
    }

    [Fact]
    public async Task ParseAsync_CachesRepeatedSentence()
    {
        var handler = new GeminiStubHandler(GeminiEnvelope("""{"listingType":"Sale"}"""));
        var parser = Build(handler);

        await parser.ParseAsync("satılık daire");
        await parser.ParseAsync("satılık daire");

        handler.Calls.ShouldBe(1); // second hit served from cache
    }
}
