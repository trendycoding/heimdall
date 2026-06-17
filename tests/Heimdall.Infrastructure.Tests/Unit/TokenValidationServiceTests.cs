using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Identity;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Unit;

/// <summary>
/// Unit tests for CustomJwtIssuer token validation in TokenValidationService.
/// Validates: Requirements 27.5 (3.5, 3.6, 3.7, 3.8)
/// </summary>
public class TokenValidationServiceTests : IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebKey _publicJwk;

    private const string ValidIssuer = "https://custom-issuer.example.com";
    private const string ValidAudience = "heimdall-api";
    private const string AllowedAlgorithm = SecurityAlgorithms.RsaSha256;

    public TokenValidationServiceTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "test-key-1" };
        _signingCredentials = new SigningCredentials(_signingKey, AllowedAlgorithm);

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

    #region Helpers

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

    #endregion

    #region Unsigned Token Rejected

    [Fact]
    public async Task ValidateTokenAsync_UnsignedToken_AlgNone_ReturnsInvalid()
    {
        // Arrange
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
        var token = CreateUnsignedToken(ValidIssuer, ValidAudience, now.AddMinutes(-1), now.AddMinutes(30));

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("nsigned", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateTokenAsync_UnsignedToken_EmptyAlg_ReturnsInvalid()
    {
        // Arrange: token with empty algorithm header
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

        // Create a token with empty algorithm
        var headerJson = System.Text.Json.JsonSerializer.Serialize(new { alg = "", typ = "JWT" });
        var headerBase64 = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(headerJson));
        var now = DateTime.UtcNow;
        var payloadObj = new Dictionary<string, object>
        {
            ["iss"] = ValidIssuer,
            ["sub"] = "user-123",
            ["aud"] = ValidAudience,
            ["nbf"] = new DateTimeOffset(now.AddMinutes(-1)).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(now.AddMinutes(30)).ToUnixTimeSeconds(),
            ["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds()
        };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);
        var payloadBase64 = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(payloadJson));
        var token = $"{headerBase64}.{payloadBase64}.";

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Weak/Disallowed Algorithm Rejected

    [Fact]
    public async Task ValidateTokenAsync_DisallowedAlgorithm_ReturnsInvalid()
    {
        // Arrange: IdP only allows RS384, but token is signed with RS256
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId, allowedAlgorithms: new List<string> { SecurityAlgorithms.RsaSha384 });
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("not permitted", result.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_AlgorithmNotInList_ReturnsInvalid()
    {
        // Arrange: IdP allows RS512 and PS256 but not RS256
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId, allowedAlgorithms: new List<string>
        {
            SecurityAlgorithms.RsaSha512,
            SecurityAlgorithms.RsaSsaPssSha256
        });
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("not permitted", result.Error);
    }

    #endregion

    #region Invalid Issuer Rejected

    [Fact]
    public async Task ValidateTokenAsync_WrongIssuer_ReturnsInvalid()
    {
        // Arrange
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
        var token = CreateToken("https://wrong-issuer.example.com", ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("issuer", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateTokenAsync_EmptyIssuer_ReturnsInvalid()
    {
        // Arrange: token has empty issuer while config expects a specific one
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
        var token = CreateToken("", ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("issuer", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Invalid Audience Rejected

    [Fact]
    public async Task ValidateTokenAsync_WrongAudience_ReturnsInvalid()
    {
        // Arrange
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
        var token = CreateToken(ValidIssuer, "wrong-audience", now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("audience", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateTokenAsync_MissingAudienceWhenRequired_ReturnsInvalid()
    {
        // Arrange: IdP config requires audience but token has none
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
        // Create a token without audience
        var token = CreateToken(ValidIssuer, null, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("audience", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Expired Lifetime Rejected

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_BeyondClockSkew_ReturnsInvalid()
    {
        // Arrange: token expired 10 minutes ago, clock skew is 5 minutes (300s)
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId, clockSkewSeconds: 300);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddHours(-2), now.AddMinutes(-10), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.True(
            result.Error.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("Lifetime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateTokenAsync_TokenNotYetValid_BeyondClockSkew_ReturnsInvalid()
    {
        // Arrange: token's NotBefore is 10 minutes in the future, clock skew is 300s (5 min)
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId, clockSkewSeconds: 300);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddMinutes(10), now.AddMinutes(60), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.True(
            result.Error.Contains("not yet valid", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("Lifetime", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Valid Token Accepted

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_AllConditionsMet_ReturnsValid()
    {
        // Arrange: correct issuer, audience, lifetime, allowed algorithm, verifiable key
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
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_WithClaimMappings_ExtractsClaims()
    {
        // Arrange: configure claim mappings using the .NET claim type URIs
        // JwtSecurityTokenHandler maps 'sub' → ClaimTypes.NameIdentifier, 'email' → ClaimTypes.Email
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId);
        idpConfig.ClaimMappings = new List<Domain.ValueObjects.ClaimMapping>
        {
            new("Subject", System.Security.Claims.ClaimTypes.NameIdentifier),
            new("Email", System.Security.Claims.ClaimTypes.Email)
        };
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddMinutes(-1), now.AddMinutes(30), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("Subject", result.Claims.Keys);
        Assert.Equal("user-123", result.Claims["Subject"]);
        Assert.Contains("Email", result.Claims.Keys);
        Assert.Equal("user@example.com", result.Claims["Email"]);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_WithinClockSkew_ReturnsValid()
    {
        // Arrange: token just expired 2 minutes ago, but clock skew allows 5 minutes
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var dbContext = CreateDbContext(tenantContext);
        var idpConfig = CreateIdpConfig(tenantId, clockSkewSeconds: 300);
        dbContext.IdentityProviderConfigurations.Add(idpConfig);
        await dbContext.SaveChangesAsync();

        var jwksJson = GetJwksJson();
        var service = CreateService(dbContext, jwksJson);

        var now = DateTime.UtcNow;
        // Token expired 2 minutes ago - should still be valid with 300s (5 min) clock skew
        var token = CreateToken(ValidIssuer, ValidAudience, now.AddHours(-1), now.AddMinutes(-2), _signingCredentials);

        // Act
        var result = await service.ValidateTokenAsync(token, tenantId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    #endregion
}
