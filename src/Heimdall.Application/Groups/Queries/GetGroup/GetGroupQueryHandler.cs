using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Groups.Queries.GetGroup;

public sealed class GetGroupQueryHandler : IRequestHandler<GetGroupQuery, GroupDto?>
{
    private readonly IRepository<Group> _repository;

    public GetGroupQueryHandler(IRepository<Group> repository)
    {
        _repository = repository;
    }

    public async Task<GroupDto?> Handle(GetGroupQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            return null;

        return new GroupDto(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
