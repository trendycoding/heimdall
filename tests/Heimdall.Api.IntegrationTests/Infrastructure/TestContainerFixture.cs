using Testcontainers.MsSql;

namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Shared fixture that manages a SQL Server Testcontainer for integration tests.
/// Uses xUnit's IAsyncLifetime so the container starts once per test collection
/// and is disposed after all tests complete.
/// </summary>
public class TestContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    /// <summary>
    /// The connection string to the running SQL Server container.
    /// Available after InitializeAsync completes.
    /// </summary>
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container has not been started.");

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test@12345!")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
