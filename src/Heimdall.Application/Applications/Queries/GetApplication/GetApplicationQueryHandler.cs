using Heimdall.Domain.Interfaces;
using MediatR;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Applications.Queries.GetApplication;

public sealed class GetApplicationQueryHandler : IRequestHandler<GetApplicationQuery, GetApplicationResult?>
{
    private readonly IRepository<ApplicationEntity> _repository;

    public GetApplicationQueryHandler(IRepository<ApplicationEntity> repository)
    {
        _repository = repository;
    }

    public async Task<GetApplicationResult?> Handle(GetApplicationQuery request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        return new GetApplicationResult(
            application.Id,
            application.TenantId,
            application.Name,
            application.Description,
            application.ClientIdentifier,
            application.AllowedRedirectUris,
            application.AllowedOrigins,
            application.Status,
            application.CreatedAt,
            application.CreatedBy,
            application.ModifiedAt,
            application.ModifiedBy);
    }
}
