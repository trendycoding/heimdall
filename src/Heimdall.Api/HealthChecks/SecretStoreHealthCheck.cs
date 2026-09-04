using Heimdall.Domain.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.HealthChecks;

/// <summary>
/// Health check that verifies the configured secret store (Key Vault, Secrets Manager,
/// or configuration) is reachable via the cloud-agnostic <see cref="ISecretProvider"/>.
/// </summary>
public sealed class SecretStoreHealthCheck : IHealthCheck
{
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<SecretStoreHealthCheck> _logger;

    public SecretStoreHealthCheck(ISecretProvider secretProvider, ILogger<SecretStoreHealthCheck> logger)
    {
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _secretProvider.IsHealthyAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("Secret store is accessible.")
                : HealthCheckResult.Unhealthy("Secret store is unreachable.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Secret store health check timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Secret store health check failed");
            return HealthCheckResult.Unhealthy($"Secret store is unreachable: {ex.Message}", exception: ex);
        }
    }
}
