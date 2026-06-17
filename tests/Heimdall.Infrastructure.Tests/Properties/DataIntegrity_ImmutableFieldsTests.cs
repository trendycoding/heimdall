using FsCheck;
using FsCheck.Xunit;
using Heimdall.Application.Applications.Commands.UpdateApplication;
using Heimdall.Application.Tenants.Commands.UpdateTenant;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 9: Immutable Fields Preserved on Update
/// Generate entity update operations with payloads including immutable field values;
/// assert those fields remain unchanged after update.
///
/// - Tenant update preserves TenantId, CreatedAt, CreatedBy
/// - Application update preserves ApplicationId, TenantId, ClientIdentifier, Status
///
/// Tests use the actual command handlers with in-memory DbContext + real Repository.
///
/// **Validates: Requirements 1.2, 2.3**
/// </summary>
public class DataIntegrity_ImmutableFieldsTests
{
    private static (HeimdallDbContext context, Repository<T> repository) CreateInfrastructure<T>(
        ITenantContext tenantContext, string? dbName = null) where T : BaseEntity
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new HeimdallDbContext(options, tenantContext);
        var repository = new Repository<T>(context);
        return (context, repository);
    }

    #region Tenant Immutable Fields

    [Property(MaxTest = 100)]
    public bool TenantUpdate_PreservesTenantId_CreatedAt_CreatedBy(
        NonEmptyString newNameRaw, NonEmptyString newSlugRaw, PositiveInt modeRaw)
    {
        // Generate arbitrary valid mutable field values
        var newName = SanitizeName(newNameRaw.Get, 128);
        var newSlug = SanitizeSlug(newSlugRaw.Get);
        var modeValues = Enum.GetValues<PrimaryIdentityMode>();
        var newMode = modeValues[modeRaw.Get % modeValues.Length];

        return RunTenantImmutableFieldsTestAsync(newName, newSlug, newMode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunTenantImmutableFieldsTestAsync(
        string newName, string newSlug, PrimaryIdentityMode newMode)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());
        tenantContext.ActorEmail.Returns("creator@test.com");

        var dbName = Guid.NewGuid().ToString();
        var (context, repository) = CreateInfrastructure<Tenant>(tenantContext, dbName);

        try
        {
            // Seed the tenant
            var seededTenant = new Tenant
            {
                Name = "Original Name",
                Slug = "original-slug",
                PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
                Status = TenantStatus.Active
            };
            await repository.AddAsync(seededTenant);

            // Capture immutable field values after creation
            var originalId = seededTenant.Id;
            var originalCreatedAt = seededTenant.CreatedAt;
            var originalCreatedBy = seededTenant.CreatedBy;

            // Switch actor for the update
            tenantContext.ActorEmail.Returns("updater@test.com");

            // Execute the actual UpdateTenantCommandHandler
            var handler = new UpdateTenantCommandHandler(repository, context);
            var command = new UpdateTenantCommand(
                TenantId: originalId,
                Name: newName,
                Slug: newSlug,
                PrimaryIdentityMode: newMode);

            var result = await handler.Handle(command, CancellationToken.None);

            // Assert: Immutable fields preserved in result
            if (result.TenantId != originalId) return false;
            if (result.CreatedAt != originalCreatedAt) return false;
            if (result.CreatedBy != originalCreatedBy) return false;

            // Verify by reading directly from the DB
            var tenantFromDb = await context.Tenants.FindAsync(originalId);
            if (tenantFromDb == null) return false;

            if (tenantFromDb.Id != originalId) return false;
            if (tenantFromDb.CreatedAt != originalCreatedAt) return false;
            if (tenantFromDb.CreatedBy != originalCreatedBy) return false;

            // Verify mutable fields actually changed
            if (tenantFromDb.Name != newName) return false;
            if (tenantFromDb.Slug != newSlug) return false;
            if (tenantFromDb.PrimaryIdentityMode != newMode) return false;

            return true;
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Property(MaxTest = 100)]
    public bool TenantUpdate_CreatedAtNeverOverwritten_EvenWithModification(
        NonEmptyString nameRaw, PositiveInt modeRaw)
    {
        // Verifies that CreatedAt is set once on creation and never modified,
        // even when ModifiedAt is updated by SaveChangesAsync.
        var newName = SanitizeName(nameRaw.Get, 128);
        var modeValues = Enum.GetValues<PrimaryIdentityMode>();
        var newMode = modeValues[modeRaw.Get % modeValues.Length];

        return RunTenantCreatedAtStableTestAsync(newName, newMode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunTenantCreatedAtStableTestAsync(string newName, PrimaryIdentityMode newMode)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());
        tenantContext.ActorEmail.Returns("creator@test.com");

        var dbName = Guid.NewGuid().ToString();
        var (context, repository) = CreateInfrastructure<Tenant>(tenantContext, dbName);

        try
        {
            var seededTenant = new Tenant
            {
                Name = "Original",
                Slug = "original-slug",
                PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
                Status = TenantStatus.Active
            };
            await repository.AddAsync(seededTenant);

            var originalCreatedAt = seededTenant.CreatedAt;
            var originalId = seededTenant.Id;

            // Perform multiple updates
            tenantContext.ActorEmail.Returns("updater@test.com");
            var handler = new UpdateTenantCommandHandler(repository, context);

            for (int i = 0; i < 3; i++)
            {
                var slug = $"slug-{Guid.NewGuid():N}"[..20];
                var command = new UpdateTenantCommand(originalId, $"{newName}-{i}", slug, newMode);
                await handler.Handle(command, CancellationToken.None);
            }

            var tenantFromDb = await context.Tenants.FindAsync(originalId);
            if (tenantFromDb == null) return false;

            // CreatedAt must never change regardless of how many updates
            if (tenantFromDb.CreatedAt != originalCreatedAt) return false;
            // ModifiedAt should be set (not null) after updates
            if (tenantFromDb.ModifiedAt == null) return false;

            return true;
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    #endregion

    #region Application Immutable Fields

    [Property(MaxTest = 100)]
    public bool ApplicationUpdate_PreservesApplicationId_TenantId_ClientIdentifier_Status(
        NonEmptyString newNameRaw, NonEmptyString descriptionRaw)
    {
        var newName = SanitizeName(newNameRaw.Get, 200);
        var newDescription = SanitizeName(descriptionRaw.Get, 500);

        return RunApplicationImmutableFieldsTestAsync(newName, newDescription).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunApplicationImmutableFieldsTestAsync(string newName, string newDescription)
    {
        var tenantId = Guid.NewGuid();
        var originalClientIdentifier = $"client-{Guid.NewGuid():N}"[..30];

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("creator@test.com");

        var dbName = Guid.NewGuid().ToString();
        var (context, repository) = CreateInfrastructure<ApplicationEntity>(tenantContext, dbName);

        try
        {
            // Seed the application
            var seededApp = new ApplicationEntity
            {
                TenantId = tenantId,
                Name = "Original Application",
                Description = "Original description",
                ClientIdentifier = originalClientIdentifier,
                AllowedRedirectUris = new List<string> { "https://original.com/callback" },
                AllowedOrigins = new List<string> { "https://original.com" },
                Status = ApplicationStatus.Active
            };
            context.Applications.Add(seededApp);
            await context.SaveChangesAsync();

            // Capture immutable field values
            var originalAppId = seededApp.Id;
            var originalTenantId = seededApp.TenantId;
            var originalClientId = seededApp.ClientIdentifier;
            var originalStatus = seededApp.Status;
            var originalCreatedAt = seededApp.CreatedAt;
            var originalCreatedBy = seededApp.CreatedBy;

            // Switch actor
            tenantContext.ActorEmail.Returns("updater@test.com");

            // Execute the actual UpdateApplicationCommandHandler
            var handler = new UpdateApplicationCommandHandler(repository);
            var command = new UpdateApplicationCommand(
                ApplicationId: originalAppId,
                Name: newName,
                Description: newDescription,
                AllowedRedirectUris: new List<string> { "https://new.com/callback", "https://other.com/cb" },
                AllowedOrigins: new List<string> { "https://new.com" });

            var result = await handler.Handle(command, CancellationToken.None);

            // Assert: Immutable fields preserved in result
            if (result.ApplicationId != originalAppId) return false;
            if (result.TenantId != originalTenantId) return false;
            if (result.ClientIdentifier != originalClientId) return false;
            if (result.Status != originalStatus) return false;
            if (result.CreatedAt != originalCreatedAt) return false;
            if (result.CreatedBy != originalCreatedBy) return false;

            // Verify by reading directly from the DB
            var appFromDb = await context.Applications.FindAsync(originalAppId);
            if (appFromDb == null) return false;

            if (appFromDb.Id != originalAppId) return false;
            if (appFromDb.TenantId != originalTenantId) return false;
            if (appFromDb.ClientIdentifier != originalClientId) return false;
            if (appFromDb.Status != originalStatus) return false;

            // Verify mutable fields changed
            if (appFromDb.Name != newName) return false;
            if (appFromDb.Description != newDescription) return false;

            return true;
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Property(MaxTest = 100)]
    public bool ApplicationUpdate_WithArbitraryRedirectUris_ImmutableFieldsUnchanged(
        PositiveInt uriCountRaw)
    {
        // Generate varying numbers of redirect URIs and origins to verify
        // immutable fields stay the same regardless of mutable payload size
        var uriCount = (uriCountRaw.Get % 20) + 1;

        return RunApplicationUriVariationTestAsync(uriCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunApplicationUriVariationTestAsync(int uriCount)
    {
        var tenantId = Guid.NewGuid();
        var originalClientIdentifier = $"client-{Guid.NewGuid():N}"[..30];

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("creator@test.com");

        var dbName = Guid.NewGuid().ToString();
        var (context, repository) = CreateInfrastructure<ApplicationEntity>(tenantContext, dbName);

        try
        {
            var seededApp = new ApplicationEntity
            {
                TenantId = tenantId,
                Name = "Test App",
                ClientIdentifier = originalClientIdentifier,
                AllowedRedirectUris = new List<string>(),
                AllowedOrigins = new List<string>(),
                Status = ApplicationStatus.Active
            };
            context.Applications.Add(seededApp);
            await context.SaveChangesAsync();

            var originalAppId = seededApp.Id;
            var originalTenantIdValue = seededApp.TenantId;
            var originalClientId = seededApp.ClientIdentifier;
            var originalStatus = seededApp.Status;

            tenantContext.ActorEmail.Returns("updater@test.com");

            // Generate arbitrary URIs
            var newUris = Enumerable.Range(0, uriCount)
                .Select(i => $"https://app{i}.example.com/callback")
                .ToList();
            var newOrigins = Enumerable.Range(0, uriCount)
                .Select(i => $"https://app{i}.example.com")
                .ToList();

            var handler = new UpdateApplicationCommandHandler(repository);
            var command = new UpdateApplicationCommand(
                ApplicationId: originalAppId,
                Name: "Updated App Name",
                Description: "Updated description",
                AllowedRedirectUris: newUris,
                AllowedOrigins: newOrigins);

            var result = await handler.Handle(command, CancellationToken.None);

            // Immutable fields must be preserved
            if (result.ApplicationId != originalAppId) return false;
            if (result.TenantId != originalTenantIdValue) return false;
            if (result.ClientIdentifier != originalClientId) return false;
            if (result.Status != originalStatus) return false;

            return true;
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    #endregion

    #region Helpers

    private static string SanitizeName(string raw, int maxLength)
    {
        var sanitized = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(maxLength).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Default Name";
        return sanitized;
    }

    private static string SanitizeSlug(string raw)
    {
        var sanitized = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == '-').Take(20).ToArray()).ToLower();
        if (string.IsNullOrEmpty(sanitized) || sanitized.Length < 3) sanitized = "abc";
        // Ensure starts/ends with alphanumeric
        sanitized = sanitized.TrimStart('-').TrimEnd('-');
        if (string.IsNullOrEmpty(sanitized)) sanitized = "abc";
        // Add unique suffix to avoid slug conflicts across test iterations
        sanitized = $"{sanitized}-{Guid.NewGuid():N}"[..20];
        return sanitized;
    }

    #endregion
}
