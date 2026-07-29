using System.Security.Cryptography;
using System.Text;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Identity;

public class ApiKeyValidationService : IApiKeyValidationService
{
    private readonly IHeimdallDbContext _dbContext;
    private readonly ILogger<ApiKeyValidationService> _logger;

    public ApiKeyValidationService(IHeimdallDbContext dbContext, ILogger<ApiKeyValidationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiKeyValidationResult { IsValid = false, Error = "API key is required." };
        }

        // Compute hash of the provided key
        var keyHash = ComputeHash(apiKey);

        // Look up by hash
        var registration = await _dbContext.ApiKeyRegistrations
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

        if (registration is null)
        {
            _logger.LogWarning("API key validation failed: key not found.");
            return new ApiKeyValidationResult { IsValid = false, Error = "Invalid API key." };
        }

        if (registration.Status != ApiKeyStatus.Active)
        {
            _logger.LogWarning("API key validation failed: key {KeyId} has status {Status}.", registration.Id, registration.Status);
            return new ApiKeyValidationResult { IsValid = false, Error = "API key is not active." };
        }

        if (registration.ExpiresAt.HasValue && registration.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("API key validation failed: key {KeyId} expired at {ExpiresAt}.", registration.Id, registration.ExpiresAt);
            return new ApiKeyValidationResult { IsValid = false, Error = "API key has expired." };
        }

        // Update last used timestamp (fire-and-forget style, don't block the request)
        registration.LastUsedAt = DateTime.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Non-critical — log and continue
            _logger.LogWarning(ex, "Failed to update LastUsedAt for API key {KeyId}.", registration.Id);
        }

        return new ApiKeyValidationResult
        {
            IsValid = true,
            TenantId = registration.TenantId,
            ApiKeyId = registration.Id,
            KeyName = registration.Name,
            Scopes = registration.Scopes.AsReadOnly()
        };
    }

    /// <summary>
    /// Generates a random API key with a recognizable prefix.
    /// Format: hmdl_{40 random chars}
    /// </summary>
    public static (string PlainKey, string Hash, string Prefix) GenerateKey()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(30);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..40];

        var plainKey = $"hmdl_{randomPart}";
        var hash = ComputeHash(plainKey);
        var prefix = plainKey[..12]; // "hmdl_XXXXXXX"

        return (plainKey, hash, prefix);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
