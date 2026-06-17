using Heimdall.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that provides access to the shared
/// WebApplicationFactory, HttpClient, and database context.
/// Tests inheriting from this class are part of the shared Integration collection.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly HeimdallWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(HeimdallWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateAuthenticatedClient();
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.EnsureDatabaseCreatedAsync();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a fresh DbContext from the test application's DI container.
    /// Useful for seeding data or asserting database state in tests.
    /// </summary>
    protected HeimdallDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HeimdallDbContext>();
    }

    /// <summary>
    /// Gets a scoped service from the DI container.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
