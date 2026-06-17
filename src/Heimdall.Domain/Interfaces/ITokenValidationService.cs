using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface ITokenValidationService
{
    Task<TokenValidationResult> ValidateTokenAsync(
        string token, Guid tenantId, Guid? applicationId = null,
        CancellationToken ct = default);
}
