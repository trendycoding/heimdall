using FsCheck;
using FsCheck.Xunit;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Application.Applications.Commands.CreateApplication;
using Heimdall.Application.FunctionalAreas.Commands.CreateFunctionalArea;
using Heimdall.Application.PermissionTypes.Commands.CreatePermissionType;
using Heimdall.Application.Permissions.Commands.CreatePermission;
using Heimdall.Application.Groups.Commands.CreateGroup;
using Heimdall.Application.Groups.Commands.AddGroupMembership;
using Heimdall.Application.PermissionTemplates.Commands.CreatePermissionTemplate;
using Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplatePermission;
using Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;
using Heimdall.Application.AccessDetails.Commands.CreateGroupAccessDetail;
using Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Application.Tests.Properties;

/// <summary>
/// Property 10: Uniqueness Constraint Enforcement
/// Generate creation/update requests that would produce duplicate values on
/// uniqueness-constrained fields; assert conflict error is returned.
///
/// **Validates: Requirements 1.4, 2.4, 3.4, 4.3, 5.2, 6.2, 7.2, 8.2, 8.4, 11.3, 12.2, 14.5, 14.8**
/// </summary>
public class UniquenessConstraint_EnforcementTests
{
    #region Test Infrastructure

    private class TestDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Domain.Entities.Application> Applications => Set<Domain.Entities.Application>();
        public DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations => Set<IdentityProviderConfiguration>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<FunctionalArea> FunctionalAreas => Set<FunctionalArea>();
        public DbSet<PermissionType> PermissionTypes => Set<PermissionType>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
        public DbSet<PermissionTemplate> PermissionTemplates => Set<PermissionTemplate>();
        public DbSet<PermissionTemplatePermission> PermissionTemplatePermissions => Set<PermissionTemplatePermission>();
        public DbSet<PermissionTemplateGroup> PermissionTemplateGroups => Set<PermissionTemplateGroup>();
        public DbSet<PermissionTemplateAccessDetail> PermissionTemplateAccessDetails => Set<PermissionTemplateAccessDetail>();
        public DbSet<UserAccessDetail> UserAccessDetails => Set<UserAccessDetail>();
        public DbSet<GroupAccessDetail> GroupAccessDetails => Set<GroupAccessDetail>();
        public DbSet<FunctionalAreaAccessRequirement> FunctionalAreaAccessRequirements => Set<FunctionalAreaAccessRequirement>();
        public DbSet<UserPermissionAssignment> UserPermissionAssignments => Set<UserPermissionAssignment>();
        public DbSet<GroupPermissionAssignment> GroupPermissionAssignments => Set<GroupPermissionAssignment>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();
        public DbSet<UserPermissionTemplateApplication> UserPermissionTemplateApplications => Set<UserPermissionTemplateApplication>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Tenant>();
            modelBuilder.Entity<Domain.Entities.Application>();
            modelBuilder.Entity<IdentityProviderConfiguration>(e =>
            {
                e.Ignore(x => x.ClaimMappings);
                e.Ignore(x => x.AllowedAlgorithms);
            });
            modelBuilder.Entity<UserProfile>();
            modelBuilder.Entity<FunctionalArea>();
            modelBuilder.Entity<PermissionType>();
            modelBuilder.Entity<Permission>();
            modelBuilder.Entity<Group>();
            modelBuilder.Entity<GroupMembership>();
            modelBuilder.Entity<PermissionTemplate>();
            modelBuilder.Entity<PermissionTemplatePermission>();
            modelBuilder.Entity<PermissionTemplateGroup>();
            modelBuilder.Entity<PermissionTemplateAccessDetail>();
            modelBuilder.Entity<UserAccessDetail>();
            modelBuilder.Entity<GroupAccessDetail>();
            modelBuilder.Entity<FunctionalAreaAccessRequirement>();
            modelBuilder.Entity<UserPermissionAssignment>();
            modelBuilder.Entity<GroupPermissionAssignment>();
            modelBuilder.Entity<AuditLog>();
            modelBuilder.Entity<ApiCallLog>();
            modelBuilder.Entity<UserPermissionTemplateApplication>();
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

    private class TestHeimdallDbContext : IHeimdallDbContext
    {
        private readonly TestDbContext _ctx;
        public TestHeimdallDbContext(TestDbContext ctx) => _ctx = ctx;

        public DbSet<Tenant> Tenants => _ctx.Tenants;
        public DbSet<Domain.Entities.Application> Applications => _ctx.Applications;
        public DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations => _ctx.IdentityProviderConfigurations;
        public DbSet<FunctionalArea> FunctionalAreas => _ctx.FunctionalAreas;
        public DbSet<PermissionType> PermissionTypes => _ctx.PermissionTypes;
        public DbSet<Permission> Permissions => _ctx.Permissions;
        public DbSet<UserProfile> UserProfiles => _ctx.UserProfiles;
        public DbSet<Group> Groups => _ctx.Groups;
        public DbSet<GroupMembership> GroupMemberships => _ctx.GroupMemberships;
        public DbSet<UserPermissionAssignment> UserPermissionAssignments => _ctx.UserPermissionAssignments;
        public DbSet<GroupPermissionAssignment> GroupPermissionAssignments => _ctx.GroupPermissionAssignments;
        public DbSet<UserAccessDetail> UserAccessDetails => _ctx.UserAccessDetails;
        public DbSet<GroupAccessDetail> GroupAccessDetails => _ctx.GroupAccessDetails;
        public DbSet<FunctionalAreaAccessRequirement> FunctionalAreaAccessRequirements => _ctx.FunctionalAreaAccessRequirements;
        public DbSet<PermissionTemplate> PermissionTemplates => _ctx.PermissionTemplates;
        public DbSet<PermissionTemplatePermission> PermissionTemplatePermissions => _ctx.PermissionTemplatePermissions;
        public DbSet<PermissionTemplateGroup> PermissionTemplateGroups => _ctx.PermissionTemplateGroups;
        public DbSet<PermissionTemplateAccessDetail> PermissionTemplateAccessDetails => _ctx.PermissionTemplateAccessDetails;
        public DbSet<AuditLog> AuditLogs => _ctx.AuditLogs;
        public DbSet<ApiCallLog> ApiCallLogs => _ctx.ApiCallLogs;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _ctx.SaveChangesAsync(cancellationToken);
    }

    private static (TestDbContext Db, TestHeimdallDbContext DbAbstraction, ITenantContext TenantCtx) CreateTestContext(Guid tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@heimdall.dev");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new TestDbContext(options, tenantContext);
        return (db, new TestHeimdallDbContext(db), tenantContext);
    }

    /// <summary>
    /// Adds an entity and sets its Id via the EF change tracker (bypassing protected setter).
    /// </summary>
    private static void AddWithId<T>(TestDbContext db, T entity, Guid id) where T : BaseEntity
    {
        db.Add(entity);
        db.Entry(entity).Property("Id").CurrentValue = id;
    }

    private static string GenerateSlug(int seed)
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var len = (Math.Abs(seed) % 10) + 3;
        var result = new char[len];
        for (int i = 0; i < len; i++)
            result[i] = chars[Math.Abs(seed + i * 7) % chars.Length];
        return new string(result);
    }

    private static string GenerateCode(int seed)
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
        var len = (Math.Abs(seed) % 15) + 3;
        var result = new char[len];
        result[0] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.Abs(seed) % 26];
        for (int i = 1; i < len; i++)
            result[i] = chars[Math.Abs(seed + i * 11) % chars.Length];
        return new string(result);
    }

    #endregion

    #region Tenant.Slug (Requirement 1.4)

    [Property(MaxTest = 100)]
    public bool TenantSlug_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunTenantSlugTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunTenantSlugTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var slug = GenerateSlug(seed);
            db.Tenants.Add(new Tenant { Name = "Existing", Slug = slug, PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId, Status = TenantStatus.Active });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<Tenant>>();
            var handler = new CreateTenantCommandHandler(repo, dba);
            var cmd = new CreateTenantCommand("New Tenant", slug, PrimaryIdentityMode.EntraExternalId);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region Application.ClientIdentifier (Requirement 2.4)

    [Property(MaxTest = 100)]
    public bool ApplicationClientId_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunAppClientIdTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunAppClientIdTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var clientId = $"client-{Math.Abs(seed) % 10000}";
            db.Applications.Add(new Domain.Entities.Application { TenantId = tenantId, Name = "Existing", ClientIdentifier = clientId, Status = ApplicationStatus.Active });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<Domain.Entities.Application>>();
            var handler = new CreateApplicationCommandHandler(repo, dba, tc);
            var cmd = new CreateApplicationCommand("New App", clientId, null, null, null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region FunctionalArea.FunctionalAreaCode (Requirement 5.2)

    [Property(MaxTest = 100)]
    public bool FunctionalAreaCode_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunFACodeTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunFACodeTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var code = GenerateCode(seed);
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            db.FunctionalAreas.Add(new FunctionalArea { TenantId = tenantId, ApplicationId = appId, FunctionalAreaCode = code, Name = "Existing", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<FunctionalArea>>();
            var handler = new CreateFunctionalAreaCommandHandler(repo, dba, tc);
            var cmd = new CreateFunctionalAreaCommand(appId, code, "New Area", null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region PermissionType.Code Case-Insensitive (Requirement 6.2)

    [Property(MaxTest = 100)]
    public bool PermissionTypeCode_DuplicateCaseInsensitive_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunPTCodeTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunPTCodeTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var code = GenerateCode(seed);
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            db.PermissionTypes.Add(new PermissionType { TenantId = tenantId, ApplicationId = appId, Code = code.ToLower(), Name = "Existing", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<PermissionType>>();
            var handler = new CreatePermissionTypeCommandHandler(repo, dba, tc);
            var cmd = new CreatePermissionTypeCommand(appId, code.ToUpper(), "New Type", null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region Permission.PermissionCode (Requirement 7.2)

    [Property(MaxTest = 100)]
    public bool PermissionCode_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunPermCodeTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunPermCodeTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var faId = Guid.NewGuid();
        var ptId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var code = GenerateCode(seed);
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            var fa = new FunctionalArea { TenantId = tenantId, ApplicationId = appId, FunctionalAreaCode = "FA1", Name = "FA", IsActive = true };
            AddWithId(db, fa, faId);
            var pt = new PermissionType { TenantId = tenantId, ApplicationId = appId, Code = "READ", Name = "Read", IsActive = true };
            AddWithId(db, pt, ptId);
            db.Permissions.Add(new Permission { TenantId = tenantId, ApplicationId = appId, FunctionalAreaId = faId, PermissionTypeId = ptId, PermissionCode = code, Name = "Existing", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<Permission>>();
            var handler = new CreatePermissionCommandHandler(repo, dba, tc);
            var cmd = new CreatePermissionCommand(appId, faId, ptId, code, "New Perm", null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region Group.Name (Requirement 8.2)

    [Property(MaxTest = 100)]
    public bool GroupName_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunGroupNameTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupNameTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var name = $"Group-{Math.Abs(seed) % 10000}";
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            db.Groups.Add(new Group { TenantId = tenantId, ApplicationId = appId, Name = name, IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<Group>>();
            var handler = new CreateGroupCommandHandler(repo, dba, tc);
            var cmd = new CreateGroupCommand(appId, name, null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region GroupMembership (Requirement 8.4)

    [Property(MaxTest = 100)]
    public bool GroupMembership_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunGroupMembershipTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupMembershipTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            var group = new Group { TenantId = tenantId, ApplicationId = appId, Name = "TestGroup", IsActive = true };
            AddWithId(db, group, groupId);
            var user = new UserProfile { TenantId = tenantId, ExternalSubjectId = $"sub-{seed}", IdentityProvider = "entra", Email = "u@t.com", DisplayName = "User", Status = UserStatus.Active };
            AddWithId(db, user, userId);
            db.GroupMemberships.Add(new GroupMembership { TenantId = tenantId, ApplicationId = appId, GroupId = groupId, UserProfileId = userId });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<GroupMembership>>();
            var handler = new AddGroupMembershipCommandHandler(repo, dba, tc);
            var cmd = new AddGroupMembershipCommand { TenantId = tenantId, ApplicationId = appId, GroupId = groupId, UserProfileId = userId };

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already a member"); }
        }
    }

    #endregion

    #region PermissionTemplate.TemplateCode (Requirement 14.5)

    [Property(MaxTest = 100)]
    public bool PermissionTemplateCode_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunTemplateCodeTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunTemplateCodeTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var code = $"TPL_{Math.Abs(seed) % 10000}";
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            db.PermissionTemplates.Add(new PermissionTemplate { TenantId = tenantId, ApplicationId = appId, TemplateCode = code, Name = "Existing", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<PermissionTemplate>>();
            var handler = new CreatePermissionTemplateCommandHandler(repo, dba, tc);
            var cmd = new CreatePermissionTemplateCommand(appId, code, "New Template", null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region PermissionTemplatePermission (Requirement 14.8)

    [Property(MaxTest = 100)]
    public bool PermissionTemplatePermission_Duplicate_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunTemplatePermTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunTemplatePermTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var template = new PermissionTemplate { TenantId = tenantId, ApplicationId = appId, TemplateCode = "TPL", Name = "Template", IsActive = true };
            AddWithId(db, template, templateId);
            var perm = new Permission { TenantId = tenantId, ApplicationId = appId, FunctionalAreaId = Guid.NewGuid(), PermissionTypeId = Guid.NewGuid(), PermissionCode = "PERM", Name = "P", IsActive = true };
            AddWithId(db, perm, permId);
            db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission { PermissionTemplateId = templateId, PermissionId = permId, Effect = Effect.Allow });
            await db.SaveChangesAsync();

            var handler = new AddPermissionTemplatePermissionCommandHandler(dba, tc);
            var cmd = new AddPermissionTemplatePermissionCommand(templateId, permId, Effect.Deny, null, null);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region IdentityProviderConfiguration Composite (Requirement 3.4)

    [Property(MaxTest = 100)]
    public bool IdpConfig_DuplicateComposite_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunIdpTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunIdpTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var providerType = (ProviderType)(Math.Abs(seed) % 8);
            var name = $"Provider-{Math.Abs(seed) % 1000}";
            db.IdentityProviderConfigurations.Add(new IdentityProviderConfiguration { TenantId = tenantId, ApplicationId = null, ProviderType = providerType, Name = name, Issuer = "https://issuer.example.com", Status = IdpStatus.Active });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<IdentityProviderConfiguration>>();
            var handler = new CreateIdentityProviderCommandHandler(repo, dba);
            var cmd = new CreateIdentityProviderCommand(tenantId, null, providerType, name, "https://other.com", null, null, null, null, null, null, 300, IdpStatus.Active);

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region UserAccessDetail Composite (Requirement 11.3)

    [Property(MaxTest = 100)]
    public bool UserAccessDetail_DuplicateComposite_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunUserAccessDetailTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunUserAccessDetailTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var accessType = $"TYPE_{Math.Abs(seed) % 100}";
            var accessCode = $"CODE_{Math.Abs(seed) % 100}";
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            var user = new UserProfile { TenantId = tenantId, ExternalSubjectId = $"sub-{seed}", IdentityProvider = "entra", Email = "u@t.com", DisplayName = "U", Status = UserStatus.Active };
            AddWithId(db, user, userId);
            db.UserAccessDetails.Add(new UserAccessDetail { TenantId = tenantId, ApplicationId = appId, UserProfileId = userId, AccessDetailType = accessType, AccessDetailCode = accessCode, AccessDetailValue = "v1", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<UserAccessDetail>>();
            var handler = new CreateUserAccessDetailCommandHandler(repo, dba, tc);
            var cmd = new CreateUserAccessDetailCommand { TenantId = tenantId, ApplicationId = appId, UserProfileId = userId, AccessDetailType = accessType, AccessDetailCode = accessCode, AccessDetailValue = "v2" };

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion

    #region GroupAccessDetail Composite (Requirement 12.2)

    [Property(MaxTest = 100)]
    public bool GroupAccessDetail_DuplicateComposite_ThrowsConflict(PositiveInt seedRaw)
    {
        return RunGroupAccessDetailTestAsync(seedRaw.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupAccessDetailTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var (db, dba, tc) = CreateTestContext(tenantId);
        using (db)
        {
            var accessType = $"TYPE_{Math.Abs(seed) % 100}";
            var accessCode = $"CODE_{Math.Abs(seed) % 100}";
            var app = new Domain.Entities.Application { TenantId = tenantId, Name = "App", ClientIdentifier = "c1", Status = ApplicationStatus.Active };
            AddWithId(db, app, appId);
            var group = new Group { TenantId = tenantId, ApplicationId = appId, Name = "G1", IsActive = true };
            AddWithId(db, group, groupId);
            db.GroupAccessDetails.Add(new GroupAccessDetail { TenantId = tenantId, ApplicationId = appId, GroupId = groupId, AccessDetailType = accessType, AccessDetailCode = accessCode, AccessDetailValue = "v1", IsActive = true });
            await db.SaveChangesAsync();

            var repo = Substitute.For<IRepository<GroupAccessDetail>>();
            var handler = new CreateGroupAccessDetailCommandHandler(repo, dba, tc);
            var cmd = new CreateGroupAccessDetailCommand { TenantId = tenantId, ApplicationId = appId, GroupId = groupId, AccessDetailType = accessType, AccessDetailCode = accessCode, AccessDetailValue = "v2" };

            try { await handler.Handle(cmd, CancellationToken.None); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains("already exists"); }
        }
    }

    #endregion
}
