using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Application.Tests.Unit.IdentityProviders;

public class CreateIdentityProviderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Create_Provider_When_No_Duplicate()
    {
        // Arrange — use an in-memory DbContext to satisfy EF Core async operations
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("admin@test.com");

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new TestDbContext(options, tenantContext);

        var repository = Substitute.For<IRepository<IdentityProviderConfiguration>>();
        var dbContextAbstraction = new TestHeimdallDbContext(dbContext);

        var handler = new CreateIdentityProviderCommandHandler(repository, dbContextAbstraction);

        var createdEntity = new IdentityProviderConfiguration
        {
            TenantId = tenantId,
            ProviderType = ProviderType.EntraExternalId,
            Name = "Test Provider",
            Issuer = "https://login.example.com",
            Audience = "api://my-app",
            ClientId = "client-123",
            AllowedAlgorithms = new List<string> { "RS256" },
            ClaimMappings = new List<ClaimMapping> { new("Subject", "sub") },
            ClockSkewToleranceSeconds = 300,
            Status = IdpStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin@test.com"
        };

        repository.AddAsync(Arg.Any<IdentityProviderConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(createdEntity);

        var command = new CreateIdentityProviderCommand(
            TenantId: tenantId,
            ApplicationId: null,
            ProviderType: ProviderType.EntraExternalId,
            Name: "Test Provider",
            Issuer: "https://login.example.com",
            Audience: "api://my-app",
            ClientId: "client-123",
            JwksEndpoint: null,
            SamlMetadataUrl: null,
            AllowedAlgorithms: new List<string> { "RS256" },
            ClaimMappings: new List<ClaimMapping> { new("Subject", "sub") },
            ClockSkewToleranceSeconds: 300,
            Status: IdpStatus.Active);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Test Provider", result.Name);
        Assert.Equal(ProviderType.EntraExternalId, result.ProviderType);
        Assert.Equal("https://login.example.com", result.Issuer);
        Assert.Equal(300, result.ClockSkewToleranceSeconds);
        Assert.Equal(IdpStatus.Active, result.Status);
        await repository.Received(1).AddAsync(Arg.Any<IdentityProviderConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Throw_When_Duplicate_Exists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("admin@test.com");

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new TestDbContext(options, tenantContext);

        // Seed an existing provider
        dbContext.IdentityProviderConfigurations.Add(new IdentityProviderConfiguration
        {
            TenantId = tenantId,
            ApplicationId = null,
            ProviderType = ProviderType.Google,
            Name = "Google Auth",
            Issuer = "https://accounts.google.com",
            Status = IdpStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var repository = Substitute.For<IRepository<IdentityProviderConfiguration>>();
        var dbContextAbstraction = new TestHeimdallDbContext(dbContext);

        var handler = new CreateIdentityProviderCommandHandler(repository, dbContextAbstraction);

        var command = new CreateIdentityProviderCommand(
            TenantId: tenantId,
            ApplicationId: null,
            ProviderType: ProviderType.Google,
            Name: "Google Auth",
            Issuer: "https://accounts.google.com",
            Audience: null,
            ClientId: null,
            JwksEndpoint: null,
            SamlMetadataUrl: null,
            AllowedAlgorithms: null,
            ClaimMappings: null,
            ClockSkewToleranceSeconds: 300,
            Status: IdpStatus.Active);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Contains("already exists", ex.Message);
        await repository.DidNotReceive().AddAsync(Arg.Any<IdentityProviderConfiguration>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Minimal DbContext for testing that satisfies EF Core async query requirements.
    /// </summary>
    private class TestDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations => Set<IdentityProviderConfiguration>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdentityProviderConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Ignore(e => e.ClaimMappings);
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = _tenantContext.ActorEmail;
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Adapter to expose the test DbContext as IHeimdallDbContext.
    /// </summary>
    private class TestHeimdallDbContext : IHeimdallDbContext
    {
        private readonly TestDbContext _context;

        public TestHeimdallDbContext(TestDbContext context) => _context = context;

        public DbSet<Tenant> Tenants => throw new NotImplementedException();
        public DbSet<Domain.Entities.Application> Applications => throw new NotImplementedException();
        public DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations => _context.IdentityProviderConfigurations;
        public DbSet<FunctionalArea> FunctionalAreas => throw new NotImplementedException();
        public DbSet<PermissionType> PermissionTypes => throw new NotImplementedException();
        public DbSet<Permission> Permissions => throw new NotImplementedException();
        public DbSet<UserProfile> UserProfiles => throw new NotImplementedException();
        public DbSet<Group> Groups => throw new NotImplementedException();
        public DbSet<GroupMembership> GroupMemberships => throw new NotImplementedException();
        public DbSet<UserPermissionAssignment> UserPermissionAssignments => throw new NotImplementedException();
        public DbSet<GroupPermissionAssignment> GroupPermissionAssignments => throw new NotImplementedException();
        public DbSet<UserAccessDetail> UserAccessDetails => throw new NotImplementedException();
        public DbSet<GroupAccessDetail> GroupAccessDetails => throw new NotImplementedException();
        public DbSet<FunctionalAreaAccessRequirement> FunctionalAreaAccessRequirements => throw new NotImplementedException();
        public DbSet<PermissionTemplate> PermissionTemplates => throw new NotImplementedException();
        public DbSet<PermissionTemplatePermission> PermissionTemplatePermissions => throw new NotImplementedException();
        public DbSet<PermissionTemplateGroup> PermissionTemplateGroups => throw new NotImplementedException();
        public DbSet<PermissionTemplateAccessDetail> PermissionTemplateAccessDetails => throw new NotImplementedException();
        public DbSet<AuditLog> AuditLogs => throw new NotImplementedException();
        public DbSet<ApiCallLog> ApiCallLogs => throw new NotImplementedException();
        public DbSet<TenantMembership> TenantMemberships => throw new NotImplementedException();
        public DbSet<ApiKeyRegistration> ApiKeyRegistrations => throw new NotImplementedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
