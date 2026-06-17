using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionTypes.Queries.GetPermissionType;

public sealed class GetPermissionTypeQueryHandler : IRequestHandler<GetPermissionTypeQuery, PermissionTypeDto?>
{
    private readonly IRepository<PermissionType> _repository;

    public GetPermissionTypeQueryHandler(IRepository<PermissionType> repository)
    {
        _repository = repository;
    }

    public async Task<PermissionTypeDto?> Handle(GetPermissionTypeQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            return null;

        return new PermissionTypeDto(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.IsSystemReserved,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
