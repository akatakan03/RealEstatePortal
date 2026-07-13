using Xunit;

namespace RealEstatePortal.IntegrationTests;

[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiTestFactory> { }

[Collection("Api")]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected readonly ApiTestFactory Factory;
    protected ApiTestBase(ApiTestFactory factory) => Factory = factory;

    public Task InitializeAsync() => Factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}