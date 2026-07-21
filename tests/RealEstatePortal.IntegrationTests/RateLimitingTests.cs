using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

[Collection("Api")]
public class RateLimitingTests
{
    private readonly ApiTestFactory _factory;

    public RateLimitingTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_BeyondTheLimit_Returns429()
    {
        // Lower the auth limit for this host only, so a few calls are enough to trip it.
        var factory = _factory.WithWebHostBuilder(b =>
            b.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = factory.CreateClient();

        var badCreds = new { email = "nobody@test.local", password = "wrong" };

        // The first three attempts are allowed through (and rejected as bad credentials).
        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", badCreds);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // The fourth within the window is throttled before it reaches the endpoint.
        var limited = await client.PostAsJsonAsync("/api/auth/login", badCreds);
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
