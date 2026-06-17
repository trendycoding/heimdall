using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with sample data for development purposes.
/// Only runs when the environment is Development and the database is empty.
/// Uses well-known GUIDs for idempotency.
/// </summary>
public class SeedDataService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(IServiceProvider serviceProvider, ILogger<SeedDataService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HeimdallDbContext>();

        // Only seed if the database has no tenants (empty database)
        var hasTenants = await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken);
        if (hasTenants)
        {
            _logger.LogInformation("Database already contains data. Skipping seed.");
            return;
        }

        _logger.LogInformation("Seeding development database with sample data...");
        await SeedAsync(dbContext, cancellationToken);
        _logger.LogInformation("Database seeding completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(HeimdallDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        const string seeder = "system@heimdall.dev";

        // --- Well-known GUIDs ---
        var tenantId = new Guid("A0000000-0000-0000-0000-000000000001");
        var applicationId = new Guid("B0000000-0000-0000-0000-000000000001");
        var idpId = new Guid("C0000000-0000-0000-0000-000000000001");

        var faIds = new[]
        {
            new Guid("D0000000-0000-0000-0000-000000000001"),
            new Guid("D0000000-0000-0000-0000-000000000002"),
            new Guid("D0000000-0000-0000-0000-000000000003")
        };

        var faarIds = new[]
        {
            new Guid("DA000000-0000-0000-0000-000000000001"),
            new Guid("DA000000-0000-0000-0000-000000000002"),
            new Guid("DA000000-0000-0000-0000-000000000003")
        };

        var ptIds = new[]
        {
            new Guid("E0000000-0000-0000-0000-000000000001"),
            new Guid("E0000000-0000-0000-0000-000000000002"),
            new Guid("E0000000-0000-0000-0000-000000000003"),
            new Guid("E0000000-0000-0000-0000-000000000004"),
            new Guid("E0000000-0000-0000-0000-000000000005")
        };

        var permIds = new[]
        {
            new Guid("F0000000-0000-0000-0000-000000000001"),
            new Guid("F0000000-0000-0000-0000-000000000002"),
            new Guid("F0000000-0000-0000-0000-000000000003"),
            new Guid("F0000000-0000-0000-0000-000000000004"),
            new Guid("F0000000-0000-0000-0000-000000000005"),
            new Guid("F0000000-0000-0000-0000-000000000006"),
            new Guid("F0000000-0000-0000-0000-000000000007"),
            new Guid("F0000000-0000-0000-0000-000000000008")
        };

        var groupIds = new[]
        {
            new Guid("AA000000-0000-0000-0000-000000000001"),
            new Guid("AA000000-0000-0000-0000-000000000002")
        };

        var userIds = new[]
        {
            new Guid("BB000000-0000-0000-0000-000000000001"),
            new Guid("BB000000-0000-0000-0000-000000000002"),
            new Guid("BB000000-0000-0000-0000-000000000003")
        };

        var templateIds = new[]
        {
            new Guid("CC000000-0000-0000-0000-000000000001"),
            new Guid("CC000000-0000-0000-0000-000000000002")
        };

        // ========== 1. Tenant ==========
        var tenant = new Tenant
        {
            Name = "Contoso Corporation",
            Slug = "contoso",
            PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
            Status = TenantStatus.Active,
            CreatedAt = now,
            CreatedBy = seeder
        };
        SetEntityId(dbContext, tenant, tenantId);

        // ========== 2. Application ==========
        var application = new Domain.Entities.Application
        {
            TenantId = tenantId,
            Name = "Contoso Portal",
            Description = "Primary web application for Contoso Corporation",
            ClientIdentifier = "contoso-portal-app",
            AllowedRedirectUris = new List<string>
            {
                "https://portal.contoso.com/callback",
                "http://localhost:3000/callback"
            },
            AllowedOrigins = new List<string>
            {
                "https://portal.contoso.com",
                "http://localhost:3000"
            },
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            CreatedBy = seeder
        };
        SetEntityId(dbContext, application, applicationId);

        // ========== 3. Identity Provider Configuration ==========
        var idp = new IdentityProviderConfiguration
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            ProviderType = ProviderType.EntraExternalId,
            Name = "Contoso Entra External ID",
            Issuer = "https://contoso.ciamlogin.com/contoso.onmicrosoft.com/v2.0",
            Audience = "api://contoso-portal",
            ClientId = "00000000-1111-2222-3333-444444444444",
            JwksEndpoint = "https://contoso.ciamlogin.com/contoso.onmicrosoft.com/discovery/v2.0/keys",
            AllowedAlgorithms = new List<string> { "RS256" },
            ClaimMappings = new List<ClaimMapping>
            {
                new("Subject", "sub"),
                new("Email", "email"),
                new("DisplayName", "name"),
                new("Groups", "groups"),
                new("Roles", "roles")
            },
            ClockSkewToleranceSeconds = 300,
            Status = IdpStatus.Active,
            CreatedAt = now,
            CreatedBy = seeder
        };
        SetEntityId(dbContext, idp, idpId);

        // ========== 4. Functional Areas ==========
        var functionalAreas = new[]
        {
            new FunctionalArea
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaCode = "ORDERS", Name = "Order Management",
                Description = "Manage customer orders and fulfillment",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new FunctionalArea
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaCode = "INVENTORY", Name = "Inventory Management",
                Description = "Track and manage product inventory",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new FunctionalArea
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaCode = "REPORTS", Name = "Reporting",
                Description = "Generate and view business reports",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < functionalAreas.Length; i++)
        {
            SetEntityId(dbContext, functionalAreas[i], faIds[i]);
        }

        // ========== 5. Functional Area Access Requirements ==========
        var faAccessReqs = new[]
        {
            new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[0], AccessDetailType = "Region",
                IsRequired = true, Description = "Regional access filter for orders",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[1], AccessDetailType = "Warehouse",
                IsRequired = true, Description = "Warehouse access filter for inventory",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[2], AccessDetailType = "Department",
                IsRequired = false, Description = "Department filter for reports",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < faAccessReqs.Length; i++)
        {
            SetEntityId(dbContext, faAccessReqs[i], faarIds[i]);
        }

        // ========== 6. Permission Types ==========
        var permissionTypes = new[]
        {
            new PermissionType
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Code = "READ", Name = "Read", Description = "View data",
                IsSystemReserved = true, IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new PermissionType
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Code = "WRITE", Name = "Write", Description = "Create and update data",
                IsSystemReserved = true, IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new PermissionType
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Code = "DELETE", Name = "Delete", Description = "Remove data",
                IsSystemReserved = false, IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new PermissionType
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Code = "APPROVE", Name = "Approve", Description = "Approve workflows",
                IsSystemReserved = false, IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new PermissionType
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Code = "EXPORT", Name = "Export", Description = "Export data",
                IsSystemReserved = false, IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < permissionTypes.Length; i++)
        {
            SetEntityId(dbContext, permissionTypes[i], ptIds[i]);
        }

        // ========== 7. Permissions (8 total) ==========
        var permissions = new[]
        {
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[0], PermissionTypeId = ptIds[0],
                PermissionCode = "ORDERS_READ", Name = "Read Orders",
                Description = "View order records", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[0], PermissionTypeId = ptIds[1],
                PermissionCode = "ORDERS_WRITE", Name = "Write Orders",
                Description = "Create and update orders", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[0], PermissionTypeId = ptIds[3],
                PermissionCode = "ORDERS_APPROVE", Name = "Approve Orders",
                Description = "Approve order workflows", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[1], PermissionTypeId = ptIds[0],
                PermissionCode = "INVENTORY_READ", Name = "Read Inventory",
                Description = "View inventory levels", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[1], PermissionTypeId = ptIds[1],
                PermissionCode = "INVENTORY_WRITE", Name = "Write Inventory",
                Description = "Update inventory records", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[1], PermissionTypeId = ptIds[2],
                PermissionCode = "INVENTORY_DELETE", Name = "Delete Inventory",
                Description = "Remove inventory records", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[2], PermissionTypeId = ptIds[0],
                PermissionCode = "REPORTS_READ", Name = "Read Reports",
                Description = "View reports", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            },
            new Permission
            {
                TenantId = tenantId, ApplicationId = applicationId,
                FunctionalAreaId = faIds[2], PermissionTypeId = ptIds[4],
                PermissionCode = "REPORTS_EXPORT", Name = "Export Reports",
                Description = "Export report data", IsActive = true,
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < permissions.Length; i++)
        {
            SetEntityId(dbContext, permissions[i], permIds[i]);
        }

        // ========== 8. Groups ==========
        var groups = new[]
        {
            new Group
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Name = "Order Managers", Description = "Team managing orders",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new Group
            {
                TenantId = tenantId, ApplicationId = applicationId,
                Name = "Inventory Staff", Description = "Warehouse and inventory team",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < groups.Length; i++)
        {
            SetEntityId(dbContext, groups[i], groupIds[i]);
        }

        // ========== 9. Users ==========
        var users = new[]
        {
            new UserProfile
            {
                TenantId = tenantId,
                ExternalSubjectId = "ext-user-001",
                IdentityProvider = "EntraExternalId",
                Email = "alice.johnson@contoso.com",
                DisplayName = "Alice Johnson",
                Status = UserStatus.Active,
                LastLoginAt = now.AddHours(-2),
                CreatedAt = now, CreatedBy = seeder
            },
            new UserProfile
            {
                TenantId = tenantId,
                ExternalSubjectId = "ext-user-002",
                IdentityProvider = "EntraExternalId",
                Email = "bob.smith@contoso.com",
                DisplayName = "Bob Smith",
                Status = UserStatus.Active,
                LastLoginAt = now.AddDays(-1),
                CreatedAt = now, CreatedBy = seeder
            },
            new UserProfile
            {
                TenantId = tenantId,
                ExternalSubjectId = "ext-user-003",
                IdentityProvider = "EntraExternalId",
                Email = "carol.white@contoso.com",
                DisplayName = "Carol White",
                Status = UserStatus.Active,
                LastLoginAt = now.AddDays(-3),
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < users.Length; i++)
        {
            SetEntityId(dbContext, users[i], userIds[i]);
        }

        // ========== 10. Group Memberships ==========
        var membershipIds = new[]
        {
            new Guid("DD000000-0000-0000-0000-000000000001"),
            new Guid("DD000000-0000-0000-0000-000000000002"),
            new Guid("DD000000-0000-0000-0000-000000000003")
        };
        var memberships = new[]
        {
            new GroupMembership
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[0], UserProfileId = userIds[0],
                CreatedAt = now, CreatedBy = seeder
            },
            new GroupMembership
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[0], UserProfileId = userIds[1],
                CreatedAt = now, CreatedBy = seeder
            },
            new GroupMembership
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[1], UserProfileId = userIds[2],
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < memberships.Length; i++)
        {
            SetEntityId(dbContext, memberships[i], membershipIds[i]);
        }

        // ========== 11. Permission Templates ==========
        var templates = new[]
        {
            new PermissionTemplate
            {
                TenantId = tenantId, ApplicationId = applicationId,
                TemplateCode = "ORDER_MANAGER",
                Name = "Order Manager Template",
                Description = "Standard permissions for order management staff",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new PermissionTemplate
            {
                TenantId = tenantId, ApplicationId = applicationId,
                TemplateCode = "INVENTORY_VIEWER",
                Name = "Inventory Viewer Template",
                Description = "Read-only access to inventory and reports",
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < templates.Length; i++)
        {
            SetEntityId(dbContext, templates[i], templateIds[i]);
        }

        // ========== 12. Permission Template Entries ==========
        // Template 1: Order Manager — permissions + group + access detail
        var tplPermIds = new[]
        {
            new Guid("CE000000-0000-0000-0000-000000000001"),
            new Guid("CE000000-0000-0000-0000-000000000002"),
            new Guid("CE000000-0000-0000-0000-000000000003")
        };
        var tplPerms = new[]
        {
            new PermissionTemplatePermission
            {
                PermissionTemplateId = templateIds[0], PermissionId = permIds[0],
                Effect = Effect.Allow, CreatedAt = now, CreatedBy = seeder
            },
            new PermissionTemplatePermission
            {
                PermissionTemplateId = templateIds[0], PermissionId = permIds[1],
                Effect = Effect.Allow, CreatedAt = now, CreatedBy = seeder
            },
            new PermissionTemplatePermission
            {
                PermissionTemplateId = templateIds[0], PermissionId = permIds[2],
                Effect = Effect.Allow, ValidFromOffsetDays = 0, ValidToOffsetDays = 365,
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < tplPerms.Length; i++)
        {
            SetEntityId(dbContext, tplPerms[i], tplPermIds[i]);
        }

        // Template 1: group entry
        var tplGroupId = new Guid("CF000000-0000-0000-0000-000000000001");
        var tplGroup = new PermissionTemplateGroup
        {
            PermissionTemplateId = templateIds[0], GroupId = groupIds[0],
            CreatedAt = now, CreatedBy = seeder
        };
        SetEntityId(dbContext, tplGroup, tplGroupId);

        // Template 1: access detail entry
        var tplAdId = new Guid("CD000000-0000-0000-0000-000000000001");
        var tplAd = new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = templateIds[0],
            AccessDetailType = "Region", AccessDetailCode = "DEFAULT_REGION",
            AccessDetailValue = "US-EAST",
            Description = "Default region assigned on template application",
            ValidFromOffsetDays = 0, ValidToOffsetDays = 365,
            CreatedAt = now, CreatedBy = seeder
        };
        SetEntityId(dbContext, tplAd, tplAdId);

        // Template 2: Inventory Viewer — permissions only
        var tplPerm2Ids = new[]
        {
            new Guid("CE000000-0000-0000-0000-000000000004"),
            new Guid("CE000000-0000-0000-0000-000000000005")
        };
        var tplPerms2 = new[]
        {
            new PermissionTemplatePermission
            {
                PermissionTemplateId = templateIds[1], PermissionId = permIds[3],
                Effect = Effect.Allow, CreatedAt = now, CreatedBy = seeder
            },
            new PermissionTemplatePermission
            {
                PermissionTemplateId = templateIds[1], PermissionId = permIds[6],
                Effect = Effect.Allow, CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < tplPerms2.Length; i++)
        {
            SetEntityId(dbContext, tplPerms2[i], tplPerm2Ids[i]);
        }

        // ========== 13. User Permission Assignments ==========
        var userAssignIds = new[]
        {
            new Guid("EE000000-0000-0000-0000-000000000001"),
            new Guid("EE000000-0000-0000-0000-000000000002"),
            new Guid("EE000000-0000-0000-0000-000000000003"),
            new Guid("EE000000-0000-0000-0000-000000000004")
        };
        var userAssignments = new[]
        {
            new UserPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[0], PermissionId = permIds[0],
                Effect = Effect.Allow, ValidFrom = now.AddDays(-30), ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new UserPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[0], PermissionId = permIds[6],
                Effect = Effect.Allow, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new UserPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[1], PermissionId = permIds[3],
                Effect = Effect.Allow, ValidFrom = null, ValidTo = now.AddDays(90),
                CreatedAt = now, CreatedBy = seeder
            },
            new UserPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[2], PermissionId = permIds[5],
                Effect = Effect.Deny, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < userAssignments.Length; i++)
        {
            SetEntityId(dbContext, userAssignments[i], userAssignIds[i]);
        }

        // ========== 14. Group Permission Assignments ==========
        var groupAssignIds = new[]
        {
            new Guid("EF000000-0000-0000-0000-000000000001"),
            new Guid("EF000000-0000-0000-0000-000000000002"),
            new Guid("EF000000-0000-0000-0000-000000000003")
        };
        var groupAssignments = new[]
        {
            new GroupPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[0], PermissionId = permIds[0],
                Effect = Effect.Allow, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new GroupPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[0], PermissionId = permIds[1],
                Effect = Effect.Allow, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new GroupPermissionAssignment
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[1], PermissionId = permIds[3],
                Effect = Effect.Allow, ValidFrom = now.AddDays(-7), ValidTo = now.AddDays(180),
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < groupAssignments.Length; i++)
        {
            SetEntityId(dbContext, groupAssignments[i], groupAssignIds[i]);
        }

        // ========== 15. User Access Details ==========
        var uadIds = new[]
        {
            new Guid("FA000000-0000-0000-0000-000000000001"),
            new Guid("FA000000-0000-0000-0000-000000000002"),
            new Guid("FA000000-0000-0000-0000-000000000003")
        };
        var userAccessDetails = new[]
        {
            new UserAccessDetail
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[0], AccessDetailType = "Region",
                AccessDetailCode = "US_EAST", AccessDetailValue = "US-EAST-1",
                Description = "Alice's primary region",
                IsActive = true, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new UserAccessDetail
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[0], AccessDetailType = "Department",
                AccessDetailCode = "SALES", AccessDetailValue = "Sales Department",
                Description = "Alice's department",
                IsActive = true, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new UserAccessDetail
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[1], AccessDetailType = "Region",
                AccessDetailCode = "US_WEST", AccessDetailValue = "US-WEST-2",
                Description = "Bob's primary region",
                IsActive = true, ValidFrom = now.AddDays(-60), ValidTo = now.AddDays(300),
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < userAccessDetails.Length; i++)
        {
            SetEntityId(dbContext, userAccessDetails[i], uadIds[i]);
        }

        // ========== 16. Group Access Details ==========
        var gadIds = new[]
        {
            new Guid("FB000000-0000-0000-0000-000000000001"),
            new Guid("FB000000-0000-0000-0000-000000000002")
        };
        var groupAccessDetails = new[]
        {
            new GroupAccessDetail
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[0], AccessDetailType = "Region",
                AccessDetailCode = "ALL_US", AccessDetailValue = "US-*",
                Description = "Order Managers can access all US regions",
                IsActive = true, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            },
            new GroupAccessDetail
            {
                TenantId = tenantId, ApplicationId = applicationId,
                GroupId = groupIds[1], AccessDetailType = "Warehouse",
                AccessDetailCode = "WH_MAIN", AccessDetailValue = "WAREHOUSE-MAIN",
                Description = "Inventory staff main warehouse access",
                IsActive = true, ValidFrom = null, ValidTo = null,
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < groupAccessDetails.Length; i++)
        {
            SetEntityId(dbContext, groupAccessDetails[i], gadIds[i]);
        }

        // ========== 17. Template Application History ==========
        var tplAppIds = new[]
        {
            new Guid("FC000000-0000-0000-0000-000000000001"),
            new Guid("FC000000-0000-0000-0000-000000000002")
        };
        var correlationId1 = new Guid("FD000000-0000-0000-0000-000000000001");
        var correlationId2 = new Guid("FD000000-0000-0000-0000-000000000002");
        var templateApplications = new[]
        {
            new UserPermissionTemplateApplication
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[0], PermissionTemplateId = templateIds[0],
                AppliedAt = now.AddDays(-10), AppliedBy = seeder,
                SourceIp = "127.0.0.1", UserAgent = "Heimdall-Seeder/1.0",
                CorrelationId = correlationId1, ApiCallLogId = null,
                CreatedAt = now.AddDays(-10), CreatedBy = seeder
            },
            new UserPermissionTemplateApplication
            {
                TenantId = tenantId, ApplicationId = applicationId,
                UserProfileId = userIds[2], PermissionTemplateId = templateIds[1],
                AppliedAt = now.AddDays(-5), AppliedBy = seeder,
                SourceIp = "127.0.0.1", UserAgent = "Heimdall-Seeder/1.0",
                CorrelationId = correlationId2, ApiCallLogId = null,
                CreatedAt = now.AddDays(-5), CreatedBy = seeder
            }
        };
        for (int i = 0; i < templateApplications.Length; i++)
        {
            SetEntityId(dbContext, templateApplications[i], tplAppIds[i]);
        }

        // ========== 18. Sample API Call Logs ==========
        var apiLogIds = new[]
        {
            new Guid("FE000000-0000-0000-0000-000000000001"),
            new Guid("FE000000-0000-0000-0000-000000000002"),
            new Guid("FE000000-0000-0000-0000-000000000003")
        };
        var apiCallLogs = new[]
        {
            new ApiCallLog
            {
                TenantId = tenantId, ApplicationId = applicationId,
                CorrelationId = Guid.NewGuid(),
                RequestId = "req-seed-001",
                HttpMethod = "GET",
                Endpoint = "/api/tenants/{tenantId}/applications/{appId}/permissions",
                RequestPath = $"/api/tenants/{tenantId}/applications/{applicationId}/permissions",
                CallerSubjectId = "ext-user-001",
                CallerClientId = "contoso-portal-app",
                SourceIp = "192.168.1.10", UserAgent = "Mozilla/5.0",
                StatusCode = 200, DurationMs = 45,
                RequestTimestamp = now.AddHours(-3),
                ResponseTimestamp = now.AddHours(-3).AddMilliseconds(45),
                CreatedAt = now.AddHours(-3), CreatedBy = seeder
            },
            new ApiCallLog
            {
                TenantId = tenantId, ApplicationId = applicationId,
                CorrelationId = Guid.NewGuid(),
                RequestId = "req-seed-002",
                HttpMethod = "POST",
                Endpoint = "/api/tenants/{tenantId}/applications/{appId}/access-checks",
                RequestPath = $"/api/tenants/{tenantId}/applications/{applicationId}/access-checks",
                CallerSubjectId = "ext-user-002",
                CallerClientId = "contoso-portal-app",
                SourceIp = "192.168.1.20", UserAgent = "axios/1.6.0",
                StatusCode = 200, DurationMs = 28,
                RequestTimestamp = now.AddHours(-2),
                ResponseTimestamp = now.AddHours(-2).AddMilliseconds(28),
                CreatedAt = now.AddHours(-2), CreatedBy = seeder
            },
            new ApiCallLog
            {
                TenantId = tenantId, ApplicationId = applicationId,
                CorrelationId = Guid.NewGuid(),
                RequestId = "req-seed-003",
                HttpMethod = "PUT",
                Endpoint = "/api/tenants/{tenantId}/users/{userId}",
                RequestPath = $"/api/tenants/{tenantId}/users/{userIds[0]}",
                CallerSubjectId = "ext-user-001",
                CallerClientId = "contoso-portal-app",
                SourceIp = "192.168.1.10", UserAgent = "Mozilla/5.0",
                StatusCode = 200, DurationMs = 112,
                RequestTimestamp = now.AddHours(-1),
                ResponseTimestamp = now.AddHours(-1).AddMilliseconds(112),
                CreatedAt = now.AddHours(-1), CreatedBy = seeder
            }
        };
        for (int i = 0; i < apiCallLogs.Length; i++)
        {
            SetEntityId(dbContext, apiCallLogs[i], apiLogIds[i]);
        }

        // ========== 19. Sample Audit Logs ==========
        var auditLogIds = new[]
        {
            new Guid("FF000000-0000-0000-0000-000000000001"),
            new Guid("FF000000-0000-0000-0000-000000000002"),
            new Guid("FF000000-0000-0000-0000-000000000003")
        };
        var auditLogs = new[]
        {
            new AuditLog
            {
                TenantId = tenantId, ApplicationId = applicationId,
                CorrelationId = correlationId1,
                ApiCallLogId = apiLogIds[2],
                ActorUserProfileId = userIds[0],
                ActorSubjectId = "ext-user-001",
                ActorEmail = "alice.johnson@contoso.com",
                EntityType = "UserPermissionAssignment",
                EntityId = userAssignIds[0].ToString(),
                Action = "Created",
                BeforeJson = null,
                AfterJson = "{\"PermissionId\":\"" + permIds[0] + "\",\"Effect\":\"Allow\"}",
                ChangedFieldsJson = null,
                SourceIp = "192.168.1.10",
                UserAgent = "Mozilla/5.0",
                CreatedAt = now.AddDays(-10), CreatedBy = seeder
            },
            new AuditLog
            {
                TenantId = tenantId, ApplicationId = applicationId,
                CorrelationId = correlationId2,
                ApiCallLogId = null,
                ActorUserProfileId = userIds[1],
                ActorSubjectId = "ext-user-002",
                ActorEmail = "bob.smith@contoso.com",
                EntityType = "UserProfile",
                EntityId = userIds[1].ToString(),
                Action = "Updated",
                BeforeJson = "{\"DisplayName\":\"Bob S.\"}",
                AfterJson = "{\"DisplayName\":\"Bob Smith\"}",
                ChangedFieldsJson = "[\"DisplayName\"]",
                SourceIp = "192.168.1.20",
                UserAgent = "axios/1.6.0",
                CreatedAt = now.AddDays(-5), CreatedBy = seeder
            },
            new AuditLog
            {
                TenantId = tenantId, ApplicationId = null,
                CorrelationId = Guid.NewGuid(),
                ApiCallLogId = null,
                ActorUserProfileId = null,
                ActorSubjectId = "system",
                ActorEmail = seeder,
                EntityType = "Tenant",
                EntityId = tenantId.ToString(),
                Action = "Created",
                BeforeJson = null,
                AfterJson = "{\"Name\":\"Contoso Corporation\",\"Slug\":\"contoso\"}",
                ChangedFieldsJson = null,
                SourceIp = "127.0.0.1",
                UserAgent = "Heimdall-Seeder/1.0",
                CreatedAt = now, CreatedBy = seeder
            }
        };
        for (int i = 0; i < auditLogs.Length; i++)
        {
            SetEntityId(dbContext, auditLogs[i], auditLogIds[i]);
        }

        // Save all seeded data
        dbContext.SuppressAutoTimestamps = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.SuppressAutoTimestamps = false;
    }

    /// <summary>
    /// Sets the Id property on a BaseEntity via EF Core's change tracker,
    /// bypassing the protected setter. The entity must be added to the context first.
    /// </summary>
    private static void SetEntityId<T>(HeimdallDbContext dbContext, T entity, Guid id) where T : BaseEntity
    {
        dbContext.Add(entity);
        dbContext.Entry(entity).Property(e => e.Id).CurrentValue = id;
    }
}
