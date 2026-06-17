using FluentValidation.TestHelper;
using Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.Tests.Unit.IdentityProviders;

public class CreateIdentityProviderCommandValidatorTests
{
    private readonly CreateIdentityProviderCommandValidator _validator = new();

    private static CreateIdentityProviderCommand CreateValidCommand() => new(
        TenantId: Guid.NewGuid(),
        ApplicationId: null,
        ProviderType: ProviderType.EntraExternalId,
        Name: "Test Provider",
        Issuer: "https://login.example.com",
        Audience: "api://my-app",
        ClientId: "client-123",
        JwksEndpoint: "https://login.example.com/.well-known/jwks",
        SamlMetadataUrl: null,
        AllowedAlgorithms: new List<string> { "RS256" },
        ClaimMappings: null,
        ClockSkewToleranceSeconds: 300,
        Status: IdpStatus.Active);

    [Fact]
    public void Should_Pass_When_Command_Is_Valid()
    {
        var command = CreateValidCommand();
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_TenantId_Is_Empty()
    {
        var command = CreateValidCommand() with { TenantId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_Fail_When_Name_Is_Empty()
    {
        var command = CreateValidCommand() with { Name = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Fail_When_Issuer_Is_Empty()
    {
        var command = CreateValidCommand() with { Issuer = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Issuer);
    }

    [Fact]
    public void Should_Fail_When_ClockSkew_Below_Zero()
    {
        var command = CreateValidCommand() with { ClockSkewToleranceSeconds = -1 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ClockSkewToleranceSeconds);
    }

    [Fact]
    public void Should_Fail_When_ClockSkew_Above_600()
    {
        var command = CreateValidCommand() with { ClockSkewToleranceSeconds = 601 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ClockSkewToleranceSeconds);
    }

    [Fact]
    public void Should_Pass_When_ClockSkew_Is_Zero()
    {
        var command = CreateValidCommand() with { ClockSkewToleranceSeconds = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ClockSkewToleranceSeconds);
    }

    [Fact]
    public void Should_Pass_When_ClockSkew_Is_600()
    {
        var command = CreateValidCommand() with { ClockSkewToleranceSeconds = 600 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ClockSkewToleranceSeconds);
    }

    [Fact]
    public void Should_Fail_When_ProviderType_Is_Invalid()
    {
        var command = CreateValidCommand() with { ProviderType = (ProviderType)999 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProviderType);
    }
}
