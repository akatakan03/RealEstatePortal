using Xunit;

namespace RealEstatePortal.IntegrationTests;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }