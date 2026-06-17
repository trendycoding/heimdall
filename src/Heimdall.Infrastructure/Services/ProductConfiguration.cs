using Heimdall.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Reads product display name from configuration (Platform:DisplayName key).
/// Falls back to "Heimdall Access" if the value is missing, blank, or exceeds 100 characters.
/// </summary>
public class ProductConfiguration : IProductConfiguration
{
    private readonly IConfiguration _config;

    public ProductConfiguration(IConfiguration config) => _config = config;

    public string GetDisplayName()
    {
        var name = _config["Platform:DisplayName"];
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return "Heimdall Access";
        return name;
    }
}
