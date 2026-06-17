using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;

public sealed class CreateUserAccessDetailCommandHandler : IRequestHandler<CreateUserAccessDetailCommand, CreateUserAccessDetailResult>
{
    private readonly IRepository<UserAccessDetail> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateUserAccessDetailCommandHandler(
        IRepository<UserAccessDetail> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateUserAccessDetailResult> Handle(CreateUserAccessDetailCommand request, CancellationToken cancellationToken)
    {
        // Validate application exists within tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate user profile exists within tenant
        var userExists = await _dbContext.UserProfiles
            .AnyAsync(u => u.Id == request.UserProfileId && u.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException($"UserProfile '{request.UserProfileId}' does not exist or belongs to a different tenant.");
        }

        // Enforce composite uniqueness: UserProfileId + ApplicationId + AccessDetailType + AccessDetailCode
        var duplicateExists = await _dbContext.UserAccessDetails
            .AnyAsync(d => d.UserProfileId == request.UserProfileId
                        && d.ApplicationId == request.ApplicationId
                        && d.AccessDetailType == request.AccessDetailType
                        && d.AccessDetailCode == request.AccessDetailCode, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"A UserAccessDetail with type '{request.AccessDetailType}' and code '{request.AccessDetailCode}' " +
                $"already exists for this user and application.");
        }

        var entity = new UserAccessDetail
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            UserProfileId = request.UserProfileId,
            AccessDetailType = request.AccessDetailType,
            AccessDetailCode = request.AccessDetailCode,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            IsActive = true,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateUserAccessDetailResult(entity.Id);
    }
}
