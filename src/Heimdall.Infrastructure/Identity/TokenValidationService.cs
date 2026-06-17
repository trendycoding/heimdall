using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TokenValidationResult = Heimdall.Domain.Models.TokenValidationResult;

namespace Heimdall.Infrastructure.Identity;

public class TokenValidationService : ITokenValidationService
{
    private readonly IHeimdallDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenValidationService> _logger;

    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(24);

    public TokenValidationService(
        IHeimdallDbContext dbContext,
        ICacheService cacheService,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenValidationService> logger)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        string token, Guid tenantId, Guid? applicationId = null,
        CancellationToken ct = default)
    {
        // 1. Resolve IdP configuration
        var idpConfig = await ResolveIdentityProviderAsync(tenantId, applicationId, token, ct);
        if (idpConfig is null)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "No matching identity provider configuration found."
            };
        }

        // 2. Reject if IdP is not active
        if (idpConfig.Status != IdpStatus.Active)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Identity provider is not active."
            };
        }

        // 3. Route to appropriate validator by ProviderType
        try
        {
            return idpConfig.ProviderType switch
            {
                ProviderType.CustomJwtIssuer => await ValidateCustomJwtAsync(token, idpConfig, ct),
                ProviderType.ExternalSaml => ValidateSamlToken(token, idpConfig),
                _ => await ValidateOidcTokenAsync(token, idpConfig, ct)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed for provider {ProviderId}", idpConfig.Id);
            return new TokenValidationResult
            {
                IsValid = false,
                Error = $"Token validation failed: {ex.Message}"
            };
        }
    }

    private async Task<IdentityProviderConfiguration?> ResolveIdentityProviderAsync(
        Guid tenantId, Guid? applicationId, string token, CancellationToken ct)
    {
        // Try to find IdP config matching tenant + application first, then tenant-level (null application)
        var query = _dbContext.IdentityProviderConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId);

        if (applicationId.HasValue)
        {
            // Try application-specific first
            var appConfig = await query
                .Where(c => c.ApplicationId == applicationId.Value)
                .FirstOrDefaultAsync(ct);

            if (appConfig is not null)
                return appConfig;
        }

        // Fallback to tenant-level (ApplicationId == null)
        var tenantConfig = await query
            .Where(c => c.ApplicationId == null)
            .FirstOrDefaultAsync(ct);

        return tenantConfig;
    }

    private async Task<TokenValidationResult> ValidateCustomJwtAsync(
        string token, IdentityProviderConfiguration config, CancellationToken ct)
    {
        var handler = new JwtSecurityTokenHandler();

        // Parse JWT header without verifying
        if (!handler.CanReadToken(token))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Token is not a valid JWT."
            };
        }

        var jwt = handler.ReadJwtToken(token);

        // Reject unsigned tokens (alg = none)
        var algorithm = jwt.Header.Alg;
        if (string.IsNullOrEmpty(algorithm) ||
            string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Unsigned tokens are not accepted."
            };
        }

        // Check algorithm is in AllowedAlgorithms list
        if (config.AllowedAlgorithms.Count > 0 &&
            !config.AllowedAlgorithms.Contains(algorithm, StringComparer.OrdinalIgnoreCase))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = $"Algorithm '{algorithm}' is not permitted."
            };
        }

        // Validate issuer
        if (!string.Equals(jwt.Issuer, config.Issuer, StringComparison.Ordinal))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Token issuer does not match configured issuer."
            };
        }

        // Validate audience
        if (!string.IsNullOrEmpty(config.Audience))
        {
            var audiences = jwt.Audiences?.ToList() ?? new List<string>();
            if (!audiences.Contains(config.Audience, StringComparer.Ordinal))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Token audience does not match configured audience."
                };
            }
        }

        // Validate lifetime with clock skew
        var clockSkew = TimeSpan.FromSeconds(
            Math.Clamp(config.ClockSkewToleranceSeconds, 0, 600));

        var now = DateTime.UtcNow;
        if (jwt.ValidFrom != DateTime.MinValue && jwt.ValidFrom > now.Add(clockSkew))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Token is not yet valid."
            };
        }

        if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo.Add(clockSkew) < now)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "Token has expired."
            };
        }

        // Verify signature via JWKS or public key
        var signingKeys = await GetSigningKeysAsync(config, ct);
        if (signingKeys is null || signingKeys.Count == 0)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = "No signing keys available for verification."
            };
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(config.Audience),
            ValidAudience = config.Audience,
            ValidateLifetime = true,
            ClockSkew = clockSkew,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys
        };

        var validationResult = await handler.ValidateTokenAsync(token, validationParameters);
        if (!validationResult.IsValid)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = validationResult.Exception?.Message ?? "Token signature validation failed."
            };
        }

        // Extract claims using ClaimMappings
        var claims = ExtractMappedClaims(validationResult.ClaimsIdentity, config);

        return new TokenValidationResult
        {
            IsValid = true,
            Claims = claims
        };
    }

    private async Task<TokenValidationResult> ValidateOidcTokenAsync(
        string token, IdentityProviderConfiguration config, CancellationToken ct)
    {
        // Build OIDC metadata address from the issuer
        var metadataAddress = config.Issuer.TrimEnd('/') + "/.well-known/openid-configuration";

        var httpClient = _httpClientFactory.CreateClient("OidcMetadata");
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClient));

        var oidcConfig = await configManager.GetConfigurationAsync(ct);

        var clockSkew = TimeSpan.FromSeconds(
            Math.Clamp(config.ClockSkewToleranceSeconds, 0, 600));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(config.Audience),
            ValidAudience = config.Audience,
            ValidateLifetime = true,
            ClockSkew = clockSkew,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidcConfig.SigningKeys
        };

        var handler = new JwtSecurityTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(token, validationParameters);

        if (!validationResult.IsValid)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = validationResult.Exception?.Message ?? "OIDC token validation failed."
            };
        }

        var claims = ExtractMappedClaims(validationResult.ClaimsIdentity, config);

        return new TokenValidationResult
        {
            IsValid = true,
            Claims = claims
        };
    }

    private static TokenValidationResult ValidateSamlToken(
        string token, IdentityProviderConfiguration config)
    {
        // SAML token validation is a placeholder — full SAML support requires
        // additional libraries (e.g., ITfoxtec.Identity.Saml2) and metadata parsing.
        // For now, return an error indicating SAML is not yet fully implemented.
        return new TokenValidationResult
        {
            IsValid = false,
            Error = "SAML token validation is not yet implemented."
        };
    }

    private async Task<IList<SecurityKey>?> GetSigningKeysAsync(
        IdentityProviderConfiguration config, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(config.JwksEndpoint))
            return null;

        var cacheKey = $"jwks:{config.Id}";

        // Try cache first
        var cachedKeys = await _cacheService.GetAsync<List<SecurityKey>>(cacheKey, ct);
        if (cachedKeys is not null)
            return cachedKeys;

        // Fetch JWKS from endpoint with retry
        var keys = await FetchJwksKeysWithRetryAsync(config.JwksEndpoint, ct);
        if (keys is not null && keys.Count > 0)
        {
            await _cacheService.SetAsync(cacheKey, keys, JwksCacheDuration, ct);
        }

        return keys;
    }

    private async Task<List<SecurityKey>?> FetchJwksKeysWithRetryAsync(
        string jwksEndpoint, CancellationToken ct, int maxRetries = 2)
    {
        var httpClient = _httpClientFactory.CreateClient("Jwks");

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetStringAsync(jwksEndpoint, ct);
                var jwks = new JsonWebKeySet(response);
                return jwks.GetSigningKeys().ToList();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch JWKS from {Endpoint}, attempt {Attempt}/{MaxRetries}",
                    jwksEndpoint, attempt + 1, maxRetries);

                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), ct);
            }
        }

        _logger.LogError("Failed to fetch JWKS from {Endpoint} after all retries", jwksEndpoint);
        return null;
    }

    private static IReadOnlyDictionary<string, string> ExtractMappedClaims(
        ClaimsIdentity identity, IdentityProviderConfiguration config)
    {
        var mappedClaims = new Dictionary<string, string>();

        foreach (var mapping in config.ClaimMappings)
        {
            var claimValue = identity.FindFirst(mapping.SourceClaimName)?.Value;
            if (claimValue is not null)
            {
                mappedClaims[mapping.PlatformField] = claimValue;
            }
        }

        return mappedClaims;
    }
}
