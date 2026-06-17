using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.HealthChecks;

/// <summary>
/// Health check that verifies Azure Key Vault accessibility by attempting to list secrets.
/// Returns Unhealthy if Key Vault is unreachable or credentials are invalid.
/// </summary>
public sealed class KeyVaultHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeyVaultHealthCheck> _logger;

    public KeyVaultHealthCheck(IConfiguration configuration, ILogger<KeyVaultHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var keyVaultUri = _configuration["KeyVault:Uri"];

        if (string.IsNullOrWhiteSpace(keyVaultUri))
        {
            return HealthCheckResult.Healthy("Key Vault not configured — skipped.");
        }

        try
        {
            var client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());

            // Attempt to list secrets (returns at least one page) to verify connectivity and auth
            await foreach (var _ in client.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                break; // We only need to verify we can reach Key Vault
            }

            return HealthCheckResult.Healthy("Key Vault is accessible.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Key Vault health check timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key Vault health check failed");
            return HealthCheckResult.Unhealthy(
                $"Key Vault is unreachable: {ex.Message}",
                exception: ex);
        }
    }
}
