using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

[Collection("Api")]
public class HealthCheckTests
{
    private readonly ApiTestFactory _factory;

    public HealthCheckTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_WhenDatabaseReachable_Returns200Healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
