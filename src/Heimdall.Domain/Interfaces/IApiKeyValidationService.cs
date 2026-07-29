using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface IApiKeyValidationService
{
    /// <summary>
    /// Validates an API key and returns the associated tenant, scopes, and metadata.
    /// </summary>
    Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken ct = default);
}

