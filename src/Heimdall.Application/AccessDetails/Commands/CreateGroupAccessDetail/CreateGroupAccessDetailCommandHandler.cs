using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Commands.CreateGroupAccessDetail;

public sealed class CreateGroupAccessDetailCommandHandler : IRequestHandler<CreateGroupAccessDetailCommand, CreateGroupAccessDetailResult>
{
    private readonly IRepository<GroupAccessDetail> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateGroupAccessDetailCommandHandler(
        IRepository<GroupAccessDetail> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateGroupAccessDetailResult> Handle(CreateGroupAccessDetailCommand request, CancellationToken cancellationToken)
    {
        // Validate application exists within tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate group exists, is within tenant, and is active — Requirement 12.3
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.ApplicationId == request.ApplicationId, cancellationToken);

        if (group is null)
        {
            throw new InvalidOperationException($"Group '{request.GroupId}' does not exist or belongs to a different application.");
        }

        if (!group.IsActive)
        {
            throw new InvalidOperationException($"Group '{request.GroupId}' is inactive. Cannot add access details to an inactive group.");
        }

        // Enforce composite uniqueness: GroupId + ApplicationId + AccessDetailType + AccessDetailCode — Requirement 12.2
        var duplicateExists = await _dbContext.GroupAccessDetails
            .AnyAsync(d => d.GroupId == request.GroupId
                        && d.ApplicationId == request.ApplicationId
                        && d.AccessDetailType == request.AccessDetailType
                        && d.AccessDetailCode == request.AccessDetailCode, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"A GroupAccessDetail with type '{request.AccessDetailType}' and code '{request.AccessDetailCode}' " +
                $"already exists for this group and application.");
        }

        var entity = new GroupAccessDetail
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            GroupId = request.GroupId,
            AccessDetailType = request.AccessDetailType,
            AccessDetailCode = request.AccessDetailCode,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            IsActive = true,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateGroupAccessDetailResult(entity.Id);
    }
}
