namespace Heimdall.Domain.Enums;

/// <summary>
/// Selects which cloud provider's adapters (secrets, configuration, telemetry)
/// are wired at startup. Defaults to Azure. AWS adapters are stubbed and can be
/// implemented by anyone deploying to AWS.
/// </summary>
public enum CloudProvider
{
    /// <summary>No cloud provider — use environment variables / local config only.</summary>
    None,

    /// <summary>Azure (Key Vault, App Configuration, Application Insights).</summary>
    Azure,

    /// <summary>AWS (Secrets Manager, AppConfig/SSM, CloudWatch). Adapters are stubs.</summary>
    Aws
}
