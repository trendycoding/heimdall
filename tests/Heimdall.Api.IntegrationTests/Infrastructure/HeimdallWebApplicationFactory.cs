using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Caching;
using Heimdall.Infrastructure.Identity;
using Heimdall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for Heimdall integration tests.
/// Replaces the SQL Server database with a Testcontainers-managed instance,
/// uses in-memory caching, and installs a test authentication handler that
/// bypasses real token validation.
/// </summary>
public class HeimdallWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly TestContainerFixture _dbFixture = new();

    /// <summary>
    /// Default tenant ID used in tests. Override via ConfigureTestAuth.
    /// </summary>
    public Guid DefaultTenantId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The connection string to the test SQL Server container.
    /// </summary>
    public string ConnectionString => _dbFixture.ConnectionString;

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbFixture.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove the real SQL Server DbContext registration
            services.RemoveAll<DbContextOptions<HeimdallDbContext>>();
            services.RemoveAll<HeimdallDbContext>();

            // Replace with Testcontainers SQL Server
            services.AddDbContext<HeimdallDbContext>((sp, options) =>
            {
                var tenantContext = sp.GetRequiredService<ITenantContext>();
                options.UseSqlServer(_dbFixture.ConnectionString);
            });

            // Replace cache service with in-memory for testing
            services.RemoveAll<ICacheService>();
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();

            // Replace TenantContext with a mutable test version
            services.RemoveAll<ITenantContext>();
            services.AddScoped<ITenantContext>(sp =>
            {
                return new TenantContext
                {
                    TenantId = DefaultTenantId,
                    ActorSubjectId = "test-subject-id",
                    ActorEmail = "test@heimdall.dev",
                    AdminScopes = new[] { "SecurityService.Admin" },
                    IsSuperAdmin = true
                };
            });

            // Install test authentication handler that bypasses token validation
            services.AddAuthentication(TestAuthHandler.AuthenticationScheme)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options =>
                    {
                        options.TenantId = DefaultTenantId;
                        options.Scopes = new List<string> { "SecurityService.Admin" };
                    });

            // Remove hosted services that shouldn't run in tests (seed data, etc.)
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>
    /// Creates an HttpClient configured with test authentication defaults.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    /// <summary>
    /// Creates an HttpClient configured for a specific tenant context.
    /// </summary>
    public HttpClient CreateClientForTenant(Guid tenantId, params string[] scopes)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId.ToString());
        if (scopes.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Scopes", string.Join(",", scopes));
        }
        return client;
    }

    /// <summary>
    /// Creates an unauthenticated HttpClient (for testing 401 scenarios).
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    /// <summary>
    /// Ensures the database schema is created and migrations are applied.
    /// Call this in test setup before executing integration tests.
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HeimdallDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Gets a scoped service from the test application's DI container.
    /// </summary>
    public T GetScopedService<T>() where T : notnull
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
