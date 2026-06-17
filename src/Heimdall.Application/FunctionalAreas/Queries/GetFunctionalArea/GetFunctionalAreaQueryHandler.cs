using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.FunctionalAreas.Queries.GetFunctionalArea;

public sealed class GetFunctionalAreaQueryHandler : IRequestHandler<GetFunctionalAreaQuery, FunctionalAreaDto?>
{
    private readonly IRepository<FunctionalArea> _repository;

    public GetFunctionalAreaQueryHandler(IRepository<FunctionalArea> repository)
    {
        _repository = repository;
    }

    public async Task<FunctionalAreaDto?> Handle(GetFunctionalAreaQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            return null;

        return new FunctionalAreaDto(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.FunctionalAreaCode,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
