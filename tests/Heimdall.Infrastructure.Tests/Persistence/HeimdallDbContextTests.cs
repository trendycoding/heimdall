using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Infrastructure.Tests.Persistence;

public class HeimdallDbContextTests : IDisposable
{
    private readonly ITenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _actorEmail = "actor@test.com";
    private readonly HeimdallDbContext _context;

    public HeimdallDbContextTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.ActorEmail.Returns(_actorEmail);

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HeimdallDbContext(options, _tenantContext);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_SetsCreatedAtAndCreatedBy()
    {
        // Arrange
        var application = new ApplicationEntity
        {
            TenantId = _tenantId,
            Name = "Test App",
            ClientIdentifier = "test-client",
            Status = ApplicationStatus.Active
        };

        _context.Applications.Add(application);

        // Act
        var beforeSave = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        // Assert
        Assert.True(application.CreatedAt >= beforeSave && application.CreatedAt <= afterSave);
        Assert.Equal(_actorEmail, application.CreatedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_SetsModifiedAtAndModifiedBy()
    {
        // Arrange
        var application = new ApplicationEntity
        {
            TenantId = _tenantId,
            Name = "Test App",
            ClientIdentifier = "test-client",
            Status = ApplicationStatus.Active
        };

        _context.Applications.Add(application);
        await _context.SaveChangesAsync();

        // Act
        application.Name = "Updated App";
        _context.Entry(application).State = EntityState.Modified;

        var beforeSave = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        // Assert
        Assert.NotNull(application.ModifiedAt);
        Assert.True(application.ModifiedAt >= beforeSave && application.ModifiedAt <= afterSave);
        Assert.Equal(_actorEmail, application.ModifiedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_DoesNotSetModifiedFields()
    {
        // Arrange
        var application = new ApplicationEntity
        {
            TenantId = _tenantId,
            Name = "Test App",
            ClientIdentifier = "test-client",
            Status = ApplicationStatus.Active
        };

        _context.Applications.Add(application);

        // Act
        await _context.SaveChangesAsync();

        // Assert
        Assert.Null(application.ModifiedAt);
        Assert.Null(application.ModifiedBy);
    }

    [Fact]
    public async Task QueryFilter_ReturnsOnlyEntitiesMatchingTenantId()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();

        var matchingApp = new ApplicationEntity
        {
            TenantId = _tenantId,
            Name = "My App",
            ClientIdentifier = "my-client",
            Status = ApplicationStatus.Active
        };

        var otherApp = new ApplicationEntity
        {
            TenantId = otherTenantId,
            Name = "Other App",
            ClientIdentifier = "other-client",
            Status = ApplicationStatus.Active
        };

        _context.Applications.Add(matchingApp);
        _context.Applications.Add(otherApp);
        await _context.SaveChangesAsync();

        // Act
        var results = await _context.Applications.ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(matchingApp.Id, results[0].Id);
    }

    [Fact]
    public async Task QueryFilter_NonTenantScopedEntities_ReturnsAll()
    {
        // Arrange - Tenant entity has no query filter
        var tenant1 = new Tenant
        {
            Name = "Tenant 1",
            Slug = "tenant-1",
            PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
            Status = TenantStatus.Active
        };

        var tenant2 = new Tenant
        {
            Name = "Tenant 2",
            Slug = "tenant-2",
            PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
            Status = TenantStatus.Active
        };

        _context.Tenants.Add(tenant1);
        _context.Tenants.Add(tenant2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _context.Tenants.ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task QueryFilter_PermissionTemplatePermission_NoFilter_ReturnsAll()
    {
        // Arrange - PermissionTemplatePermission is BaseEntity (no tenant filter)
        var ptp1 = new PermissionTemplatePermission
        {
            PermissionTemplateId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow
        };

        var ptp2 = new PermissionTemplatePermission
        {
            PermissionTemplateId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Deny
        };

        _context.PermissionTemplatePermissions.Add(ptp1);
        _context.PermissionTemplatePermissions.Add(ptp2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _context.PermissionTemplatePermissions.ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditFieldsApplyToBaseEntity_NotJustTenantScoped()
    {
        // Arrange - PermissionTemplatePermission inherits BaseEntity directly
        var ptp = new PermissionTemplatePermission
        {
            PermissionTemplateId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow
        };

        _context.PermissionTemplatePermissions.Add(ptp);

        // Act
        var beforeSave = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        // Assert - audit fields should still be set on BaseEntity derivatives
        Assert.True(ptp.CreatedAt >= beforeSave && ptp.CreatedAt <= afterSave);
        Assert.Equal(_actorEmail, ptp.CreatedBy);
    }
}
