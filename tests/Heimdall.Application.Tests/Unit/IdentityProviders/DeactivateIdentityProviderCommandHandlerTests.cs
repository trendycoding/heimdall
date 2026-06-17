using Heimdall.Application.IdentityProviders.Commands.DeactivateIdentityProvider;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using NSubstitute;

namespace Heimdall.Application.Tests.Unit.IdentityProviders;

public class DeactivateIdentityProviderCommandHandlerTests
{
    private readonly IRepository<IdentityProviderConfiguration> _repository;
    private readonly DeactivateIdentityProviderCommandHandler _handler;

    public DeactivateIdentityProviderCommandHandlerTests()
    {
        _repository = Substitute.For<IRepository<IdentityProviderConfiguration>>();
        _handler = new DeactivateIdentityProviderCommandHandler(_repository);
    }

    [Fact]
    public async Task Handle_Should_Set_Status_To_Inactive()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var entity = new IdentityProviderConfiguration
        {
            TenantId = Guid.NewGuid(),
            Name = "Provider",
            Issuer = "https://issuer.example.com",
            ProviderType = ProviderType.Google,
            Status = IdpStatus.Active
        };

        _repository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(entity);

        var command = new DeactivateIdentityProviderCommand(providerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(IdpStatus.Inactive, entity.Status);
        await _repository.Received(1).UpdateAsync(entity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Provider_Not_Found()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        _repository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns((IdentityProviderConfiguration?)null);

        var command = new DeactivateIdentityProviderCommand(providerId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(providerId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Provider_Already_Inactive()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var entity = new IdentityProviderConfiguration
        {
            TenantId = Guid.NewGuid(),
            Name = "Provider",
            Issuer = "https://issuer.example.com",
            ProviderType = ProviderType.ExternalOidc,
            Status = IdpStatus.Inactive
        };

        _repository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(entity);

        var command = new DeactivateIdentityProviderCommand(providerId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("already inactive", ex.Message);
    }
}
