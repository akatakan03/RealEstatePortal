using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingsApiIntegrationTests : ApiTestBase
{
    public ListingsApiIntegrationTests(ApiTestFactory factory) : base(factory) { }

    [Fact]
    public async Task GetListings_IsPublic_ReturnsOk_WithoutToken()
    {
        var client = Factory.CreateClient();
        var resp = await client.GetAsync("/api/listings");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithBadPassword_Returns401()
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiTestFactory.AgentAEmail, password = "wrong-password" });
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateListing_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/listings", ValidCreateBody());
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateListing_WithAgentToken_Returns201()
    {
        var client = await Factory.CreateAgentClientAsync(ApiTestFactory.AgentAEmail);
        var resp = await client.PostAsJsonAsync("/api/listings", ValidCreateBody());
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateListing_WithInvalidBody_Returns400()
    {
        var client = await Factory.CreateAgentClientAsync(ApiTestFactory.AgentAEmail);
        var resp = await client.PostAsJsonAsync("/api/listings", ValidCreateBody() with { Title = "" });
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateListing_ByADifferentAgent_Returns403()
    {
        // Agent A creates a listing.
        var clientA = await Factory.CreateAgentClientAsync(ApiTestFactory.AgentAEmail);
        var createResp = await clientA.PostAsJsonAsync("/api/listings", ValidCreateBody());
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedDto>();

        // Agent B tries to update A's listing -> 403 (ownership enforced over HTTP).
        var clientB = await Factory.CreateAgentClientAsync(ApiTestFactory.AgentBEmail);
        var updateResp = await clientB.PutAsJsonAsync($"/api/listings/{created!.Id}", ValidUpdateBody());
        updateResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ListingType=1 (Sale), PropertyType=1 (Apartment) — numeric enums bind by default.
    private static Body ValidCreateBody() =>
        new("API flat", "A description", 100000m, "TRY", 1, 1, 2, 1, 90m, "Kadıköy, İstanbul", null, null);

    private static Body ValidUpdateBody() =>
        new("Updated", "A description", 120000m, "TRY", 1, 1, 2, 1, 90m, "Kadıköy, İstanbul", null, null);

    private record Body(
        string Title, string Description, decimal Price, string Currency,
        int ListingType, int PropertyType, int Bedrooms, int Bathrooms,
        decimal AreaSqMeters, string Address, double? Latitude, double? Longitude);

    private record CreatedDto(int Id);
}