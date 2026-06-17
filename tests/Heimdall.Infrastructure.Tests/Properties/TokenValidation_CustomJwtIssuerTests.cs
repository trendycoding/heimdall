using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Identity;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 22: CustomJwtIssuer Token Validation
/// Generate tokens with varying signatures, algorithms, issuers, audiences, and lifetimes;
/// assert accept iff all conditions met (signed, allowed algorithm, correct issuer, correct audience,
/// valid lifetime, verifiable key).
///
/// **Validates: Requirements 3.5, 3.6, 3.7, 3.8**
/// </summary>
public class TokenValidation_CustomJwtIssuerTests : IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebKey _publicJwk;

    private const string ValidIssuer = "https://custom-issuer.example.com";
    private const string ValidAudience = "heimdall-api";
    private const string AllowedAlgorithm = SecurityAlgorithms.RsaSha256;

    public TokenValidation_CustomJwtIssuerTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "test-key-1" };
        _signingCredentials = new SigningCredentials(_signingKey, AllowedAlgorithm);

        // Create public JWK for JWKS endpoint simulation
        var rsaParams = _rsa.ExportParameters(false);
        _publicJwk = new JsonWebKey
        {
            Kty = "RSA",
            Kid = "test-key-1",
            Use = "sig",
            N = Base64UrlEncoder.Encode(rsaParams.Modulus!),
            E = Base64UrlEncoder.Encode(rsaParams.Exponent!)
        };
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }

    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    private string CreateToken(
        string issuer,
        string? audience,
        DateTime notBefore,
        DateTime expires,
        SigningCredentials? credentials)
    {
        var handler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "user-123"),
            new(JwtRegisteredClaimNames.Email, "user@example.com")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = credentials
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    private static string CreateUnsignedToken(string issuer, string? audience, DateTime notBefore, DateTime expires)
    {
        // Manually construct an unsigned JWT (alg=none) as a raw Base64Url-encoded string
        // since the library won't create tokens with SecurityAlgorithms.None
        var headerJson = System.Text.Json.JsonSerializer.Serialize(new { alg = "none", typ = "JWT" });
        var headerBase64 = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(headerJson));

        var nbfEpoch = new DateTimeOffset(notBefore).ToUnixTimeSeconds();
        var expEpoch = new DateTimeOffset(expires).ToUnixTimeSeconds();
        var iatEpoch = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

        var payloadObj = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["sub"] = "user-123",
            ["email"] = "user@example.com",
            ["nbf"] = nbfEpoch,
            ["exp"] = expEpoch,
            ["iat"] = iatEpoch
        };

        if (audience != null)
        {
            payloadObj["aud"] = audience;
        }

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);
        var payloadBase64 = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(payloadJson));

        // Unsigned JWT: header.payload. (empty signature)
        return $"{headerBase64}.{payloadBase64}.";
    }

    private TokenValidationService CreateService(
        HeimdallDbContext dbContext,
        string? jwksResponse = null)
    {
        var cacheService = Substitute.For<ICacheService>();
        cacheService.GetAsync<List<SecurityKey>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<SecurityKey>?)null);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();

        if (jwksResponse != null)
        {
            var messageHandler = new FakeHttpMessageHandler(jwksResponse);
            var httpClient = new HttpClient(messageHandler);
            httpClientFactory.CreateClient("Jwks").Returns(httpClient);

            // Also set up cache to return signing keys directly for simpler testing
            var jwks = new JsonWebKeySet(jwksResponse);
            var keys = jwks.GetSigningKeys().ToList();
            cacheService.GetAsync<List<SecurityKey>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(keys);
        }

        var logger = NullLogger<TokenValidationService>.Instance;

        return new TokenValidationService(dbContext, cacheService, httpClientFactory, logger);
    }

    private IdentityProviderConfiguration CreateIdpConfig(
        Guid tenantId,
        Guid? applicationId = null,
        string issuer = ValidIssuer,
        string? audience = ValidAudience,
        List<string>? allowedAlgorithms = null,
        int clockSkewSeconds = 300,
        IdpStatus status = IdpStatus.Active)
    {
        return new IdentityProviderConfiguration
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            ProviderType = ProviderType.CustomJwtIssuer,
            Name = "Test Custom JWT Issuer",
            Issuer = issuer,
            Audience = audience,
            AllowedAlgorithms = allowedAlgorithms ?? new List<string> { AllowedAlgorithm },
            ClockSkewToleranceSeconds = clockSkewSeconds,
            JwksEndpoint = "https://custom-issuer.example.com/.well-known/jwks.json",
            Status = status
        };
    }

    private string GetJwksJson()
    {
        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(_publicJwk);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = _publicJwk.Kty,
                    kid = _publicJwk.Kid,
                    use = _publicJwk.Use,
                    n = _publicJwk.N,
                    e = _publicJwk.E
                }
            }
        });
    }

    /// <summary>
    /// Property: Valid token (all conditions met) → accepted.
    /// When a token is signed with an allowed algorithm, has correct issuer, audience,
    /// and valid lifetime, it should be accepted.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool ValidToken_AllConditionsMet_IsAccepted(PositiveInt lifetimeMinutesRaw)
    {
        var lifetimeMinutes = (lifetimeMinutesRaw.Get % 60) + 1;
        return RunValidTokenTestAsync(lifetimeMinutes).GetAwaiter().GetResult();
    }

    private async Task<bool> RunValidTokenTestAsync(int lifetimeMinutes)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        var idpConfig = CreateIdpConfig(tenantId);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(lifetimeMinutes),
            credentials: _signingCredentials);

        var result = await service.ValidateTokenAsync(token, tenantId);

        return result.IsValid;
    }

    /// <summary>
    /// Property: Unsigned token (alg=none) → rejected.
    /// Requirement 3.5: unsigned tokens are not accepted.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool UnsignedToken_AlgNone_IsRejected(PositiveInt lifetimeMinutesRaw)
    {
        var lifetimeMinutes = (lifetimeMinutesRaw.Get % 60) + 1;
        return RunUnsignedTokenTestAsync(lifetimeMinutes).GetAwaiter().GetResult();
    }

    private async Task<bool> RunUnsignedTokenTestAsync(int lifetimeMinutes)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        var idpConfig = CreateIdpConfig(tenantId);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateUnsignedToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(lifetimeMinutes));

        var result = await service.ValidateTokenAsync(token, tenantId);

        return !result.IsValid && result.Error != null && result.Error.Contains("nsigned");
    }

    /// <summary>
    /// Property: Disallowed algorithm → rejected.
    /// Requirement 3.6: tokens using disallowed algorithms are rejected.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool DisallowedAlgorithm_IsRejected(PositiveInt lifetimeMinutesRaw)
    {
        var lifetimeMinutes = (lifetimeMinutesRaw.Get % 60) + 1;
        return RunDisallowedAlgorithmTestAsync(lifetimeMinutes).GetAwaiter().GetResult();
    }

    private async Task<bool> RunDisallowedAlgorithmTestAsync(int lifetimeMinutes)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        // Configure IdP with RS384 only allowed, but sign with RS256
        var idpConfig = CreateIdpConfig(
            tenantId,
            allowedAlgorithms: new List<string> { SecurityAlgorithms.RsaSha384 });
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        // Sign with RS256 which is NOT in the allowed list (only RS384 is allowed)
        var token = CreateToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(lifetimeMinutes),
            credentials: _signingCredentials); // RS256

        var result = await service.ValidateTokenAsync(token, tenantId);

        return !result.IsValid && result.Error != null && result.Error.Contains("not permitted");
    }

    /// <summary>
    /// Property: Wrong issuer → rejected.
    /// Requirement 3.7: token issuer must match configured issuer.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool WrongIssuer_IsRejected(NonEmptyString wrongIssuerRaw)
    {
        var wrongIssuer = $"https://{wrongIssuerRaw.Get.Replace(" ", "")}.wrong.com";
        if (wrongIssuer == ValidIssuer) return true; // Skip if accidentally matches

        return RunWrongIssuerTestAsync(wrongIssuer).GetAwaiter().GetResult();
    }

    private async Task<bool> RunWrongIssuerTestAsync(string wrongIssuer)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        var idpConfig = CreateIdpConfig(tenantId);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(
            issuer: wrongIssuer,
            audience: ValidAudience,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            credentials: _signingCredentials);

        var result = await service.ValidateTokenAsync(token, tenantId);

        return !result.IsValid && result.Error != null && result.Error.Contains("issuer");
    }

    /// <summary>
    /// Property: Wrong audience → rejected.
    /// Requirement 3.7: token audience must match configured audience.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool WrongAudience_IsRejected(NonEmptyString wrongAudienceRaw)
    {
        var wrongAudience = wrongAudienceRaw.Get.Replace(" ", "");
        if (wrongAudience == ValidAudience) return true; // Skip if accidentally matches

        return RunWrongAudienceTestAsync(wrongAudience).GetAwaiter().GetResult();
    }

    private async Task<bool> RunWrongAudienceTestAsync(string wrongAudience)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        var idpConfig = CreateIdpConfig(tenantId);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(
            issuer: ValidIssuer,
            audience: wrongAudience,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            credentials: _signingCredentials);

        var result = await service.ValidateTokenAsync(token, tenantId);

        return !result.IsValid && result.Error != null && result.Error.Contains("audience");
    }

    /// <summary>
    /// Property: Expired token → rejected.
    /// Requirement 3.7/3.8: token lifetime must be valid (considering clock skew).
    /// </summary>
    [Property(MaxTest = 50)]
    public bool ExpiredToken_IsRejected(PositiveInt minutesPastExpiryRaw)
    {
        // Token expired between 6 and 65 minutes ago (beyond 300s clock skew)
        var minutesPastExpiry = (minutesPastExpiryRaw.Get % 60) + 6;
        return RunExpiredTokenTestAsync(minutesPastExpiry).GetAwaiter().GetResult();
    }

    private async Task<bool> RunExpiredTokenTestAsync(int minutesPastExpiry)
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);

        var idpConfig = CreateIdpConfig(tenantId);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            notBefore: now.AddHours(-2),
            expires: now.AddMinutes(-minutesPastExpiry),
            credentials: _signingCredentials);

        var result = await service.ValidateTokenAsync(token, tenantId);

        return !result.IsValid && result.Error != null &&
               (result.Error.Contains("expired") || result.Error.Contains("Lifetime"));
    }

    /// <summary>
    /// HTTP message handler that returns a fixed JWKS response for testing.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;

        public FakeHttpMessageHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_response, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
