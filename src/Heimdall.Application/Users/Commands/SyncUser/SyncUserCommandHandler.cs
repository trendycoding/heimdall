using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Users.Commands.SyncUser;

public sealed class SyncUserCommandHandler : IRequestHandler<SyncUserCommand, SyncUserResult>
{
    private readonly IRepository<UserProfile> _userRepository;
    private readonly IRepository<IdentityProviderConfiguration> _idpRepository;
    private readonly IRepository<PermissionTemplate> _templateRepository;
    private readonly ITemplateApplicationService _templateService;

    public SyncUserCommandHandler(
        IRepository<UserProfile> userRepository,
        IRepository<IdentityProviderConfiguration> idpRepository,
        IRepository<PermissionTemplate> templateRepository,
        ITemplateApplicationService templateService)
    {
        _userRepository = userRepository;
        _idpRepository = idpRepository;
        _templateRepository = templateRepository;
        _templateService = templateService;
    }

    public async Task<SyncUserResult> Handle(SyncUserCommand request, CancellationToken cancellationToken)
    {
        // Validate that the identity provider is configured for the tenant
        await ValidateIdentityProviderAsync(request, cancellationToken);

        // Validate template codes if applications are provided
        if (request.Applications.Count > 0)
        {
            await ValidateTemplateCodesAsync(request, cancellationToken);
        }

        // Upsert user profile
        var (userProfile, isNew) = await UpsertUserProfileAsync(request, cancellationToken);

        // Set EntityId for audit trail
        request.EntityId = userProfile.Id;

        // Apply templates if provided
        int templatesApplied = 0;
        if (request.Applications.Count > 0)
        {
            templatesApplied = await ApplyTemplatesAsync(request, userProfile.Id, cancellationToken);
        }

        return new SyncUserResult
        {
            UserProfileId = userProfile.Id,
            IsNewUser = isNew,
            TemplatesApplied = templatesApplied
        };
    }

    private async Task ValidateIdentityProviderAsync(SyncUserCommand request, CancellationToken ct)
    {
        var allIdps = await _idpRepository.GetAllAsync(ct);
        var configured = allIdps.Any(idp =>
            idp.TenantId == request.TenantId &&
            idp.Name == request.IdentityProvider &&
            idp.Status == IdpStatus.Active);

        if (!configured)
        {
            throw new InvalidOperationException(
                $"Identity provider '{request.IdentityProvider}' is not configured for the tenant.");
        }
    }

    private async Task ValidateTemplateCodesAsync(SyncUserCommand request, CancellationToken ct)
    {
        var allTemplates = await _templateRepository.GetAllAsync(ct);

        foreach (var appEntry in request.Applications)
        {
            foreach (var templateCode in appEntry.PermissionTemplateCodes)
            {
                var template = allTemplates.FirstOrDefault(t =>
                    t.ApplicationId == appEntry.ApplicationId &&
                    t.TemplateCode == templateCode);

                if (template is null)
                {
                    throw new InvalidOperationException(
                        $"Permission template '{templateCode}' does not exist for application '{appEntry.ApplicationId}'.");
                }

                if (!template.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Permission template '{templateCode}' is inactive for application '{appEntry.ApplicationId}'.");
                }
            }
        }
    }

    private async Task<(UserProfile Profile, bool IsNew)> UpsertUserProfileAsync(
        SyncUserCommand request, CancellationToken ct)
    {
        var allUsers = await _userRepository.GetAllAsync(ct);
        var existing = allUsers.FirstOrDefault(u =>
            u.TenantId == request.TenantId &&
            u.ExternalSubjectId == request.ExternalSubjectId &&
            u.IdentityProvider == request.IdentityProvider);

        if (existing is not null)
        {
            // Update existing user
            existing.Email = request.Email;
            existing.DisplayName = request.DisplayName;
            await _userRepository.UpdateAsync(existing, ct);
            return (existing, false);
        }

        // Create new user
        var newUser = new UserProfile
        {
            TenantId = request.TenantId,
            ExternalSubjectId = request.ExternalSubjectId,
            IdentityProvider = request.IdentityProvider,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Status = UserStatus.Active
        };

        var created = await _userRepository.AddAsync(newUser, ct);
        return (created, true);
    }

    private async Task<int> ApplyTemplatesAsync(
        SyncUserCommand request, Guid userProfileId, CancellationToken ct)
    {
        int count = 0;
        var allTemplates = await _templateRepository.GetAllAsync(ct);

        foreach (var appEntry in request.Applications)
        {
            foreach (var templateCode in appEntry.PermissionTemplateCodes)
            {
                var template = allTemplates.First(t =>
                    t.ApplicationId == appEntry.ApplicationId &&
                    t.TemplateCode == templateCode);

                await _templateService.ApplyTemplateAsync(
                    request.TenantId,
                    appEntry.ApplicationId,
                    userProfileId,
                    template.Id,
                    new TemplateApplicationOptions(),
                    ct);

                count++;
            }
        }

        return count;
    }
}
