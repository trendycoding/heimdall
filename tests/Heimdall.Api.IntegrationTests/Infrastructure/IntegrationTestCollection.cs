namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection definition that shares a single HeimdallWebApplicationFactory
/// across all integration tests. Tests in this collection share the same SQL Server
/// container and web application, improving test execution speed.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<HeimdallWebApplicationFactory>
{
    public const string Name = "Integration";
}
