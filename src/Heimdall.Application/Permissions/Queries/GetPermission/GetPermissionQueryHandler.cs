using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Permissions.Queries.GetPermission;

public sealed class GetPermissionQueryHandler : IRequestHandler<GetPermissionQuery, PermissionDto?>
{
    private readonly IRepository<Permission> _repository;

    public GetPermissionQueryHandler(IRepository<Permission> repository)
    {
        _repository = repository;
    }

    public async Task<PermissionDto?> Handle(GetPermissionQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            return null;

        return new PermissionDto(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.FunctionalAreaId,
            entity.PermissionTypeId,
            entity.PermissionCode,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
