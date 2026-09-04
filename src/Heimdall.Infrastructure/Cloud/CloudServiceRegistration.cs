using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Cloud.Aws;
using Heimdall.Infrastructure.Cloud.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Infrastructure.Cloud;

/// <summary>
/// Registers cloud-provider-specific adapters (secrets, and future config/telemetry)
/// behind provider-agnostic abstractions. The active provider is selected by the
/// <c>Cloud:Provider</c> configuration setting (Azure | Aws | None). Defaults to Azure.
/// </summary>
public static class CloudServiceRegistration
{
    public static IServiceCollection AddCloudServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = ResolveProvider(configuration);

        services.AddMemoryCache();

        switch (provider)
        {
            case CloudProvider.Aws:
                services.AddSingleton<ISecretProvider, AwsSecretsManagerProvider>();
                break;

            case CloudProvider.None:
                services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
                break;

            case CloudProvider.Azure:
            default:
                services.AddSingleton<ISecretProvider, AzureKeyVaultSecretProvider>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Reads the configured cloud provider. Defaults to Azure for backward compatibility.
    /// </summary>
    public static CloudProvider ResolveProvider(IConfiguration configuration)
    {
        var raw = configuration["Cloud:Provider"];
        if (!string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse<CloudProvider>(raw, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return CloudProvider.Azure;
    }
}
