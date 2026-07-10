using Xunit;

namespace RealEstatePortal.IntegrationTests;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFixture Fixture;

    protected IntegrationTestBase(IntegrationTestFixture fixture) => Fixture = fixture;

    public Task InitializeAsync() => Fixture.ResetStateAsync();  // clean DB before each test
    public Task DisposeAsync() => Task.CompletedTask;
}