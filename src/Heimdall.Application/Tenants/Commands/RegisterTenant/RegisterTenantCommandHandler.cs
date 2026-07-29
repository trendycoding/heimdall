using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Handles self-service tenant registration.
/// Creates a Tenant, a TenantMembership (Owner), and an initial UserProfile in one transaction.
/// This handler bypasses normal tenant-scoped operations since the tenant doesn't exist yet.
/// </summary>
public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    private readonly IHeimdallDbContext _dbContext;

    public RegisterTenantCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        // Check slug uniqueness
        var slugExists = await _dbContext.Tenants
            .AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugExists)
        {
            throw new InvalidOperationException($"A tenant with slug '{request.Slug}' already exists.");
        }

        // Check if this user already owns a tenant with the same slug pattern (prevent abuse)
        var existingMemberships = await _dbContext.TenantMemberships
            .CountAsync(m => m.ExternalSubjectId == request.ExternalSubjectId
                          && m.Role == TenantRole.Owner
                          && m.Status == MembershipStatus.Active, cancellationToken);

        // Allow up to 5 owned tenants per user (reasonable SaaS limit)
        if (existingMemberships >= 5)
        {
            throw new InvalidOperationException(
                "You have reached the maximum number of owned tenants. Please contact support for assistance.");
        }

        var now = DateTime.UtcNow;

        // Create the tenant
        var tenant = new Tenant
        {
            Name = request.TenantName,
            Slug = request.Slug,
            PrimaryIdentityMode = request.PrimaryIdentityMode,
            Status = TenantStatus.Active,
            CreatedAt = now,
            CreatedBy = request.Email
        };

        _dbContext.Tenants.Add(tenant);

        // Create the ownership membership
        var membership = new TenantMembership
        {
            TenantId = tenant.Id,
            ExternalSubjectId = request.ExternalSubjectId,
            Email = request.Email,
            Role = TenantRole.Owner,
            Status = MembershipStatus.Active,
            AcceptedAt = now,
            CreatedAt = now,
            CreatedBy = request.Email
        };

        _dbContext.TenantMemberships.Add(membership);

        // Create the user profile within the new tenant
        var userProfile = new UserProfile
        {
            TenantId = tenant.Id,
            ExternalSubjectId = request.ExternalSubjectId,
            IdentityProvider = "AzureAD", // Default; can be refined later
            Email = request.Email,
            DisplayName = request.DisplayName,
            Status = UserStatus.Active,
            LastLoginAt = now,
            CreatedAt = now,
            CreatedBy = request.Email
        };

        _dbContext.UserProfiles.Add(userProfile);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            membership.Id,
            membership.Role);
    }
}
