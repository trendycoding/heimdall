using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Caching;
using Heimdall.Infrastructure.Identity;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Heimdall.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure layer services, repositories, and data access
    /// into the DI container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("HeimdallDb");
        services.AddDbContext<HeimdallDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IHeimdallDbContext>(sp => sp.GetRequiredService<HeimdallDbContext>());

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Identity / Tenant
        services.AddScoped<ITenantContext, TenantContext>();

        // Core services
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IAccessDetailResolver, AccessDetailResolver>();
        services.AddScoped<ITemplateApplicationService, TemplateApplicationService>();
        services.AddScoped<ITokenValidationService, TokenValidationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IApiCallLogService, ApiCallLogService>();
        services.AddScoped<IEntityStateProvider, EntityStateProvider>();
        services.AddSingleton<IProductConfiguration, ProductConfiguration>();

        // Caching — use Redis when connection string is configured, otherwise in-memory
        // Redis uses a ResilientCacheService wrapper with circuit breaker + fallback to cache miss
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ICacheService, ResilientCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }

        // Key Vault resilience — local memory cache of secrets with retry on transient failures
        services.AddMemoryCache();
        services.AddSingleton<ResilientKeyVaultProvider>();

        return services;
    }

    /// <summary>
    /// Registers the development seed data service as a hosted service.
    /// Should only be called when ASPNETCORE_ENVIRONMENT is Development.
    /// </summary>
    public static IServiceCollection AddDevelopmentSeedData(this IServiceCollection services)
    {
        services.AddHostedService<SeedDataService>();
        return services;
    }
}
