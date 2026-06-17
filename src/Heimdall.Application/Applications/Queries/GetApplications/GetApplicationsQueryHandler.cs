using Heimdall.Domain.Interfaces;
using MediatR;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Applications.Queries.GetApplications;

public sealed class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, IReadOnlyList<GetApplicationsResult>>
{
    private readonly IRepository<ApplicationEntity> _repository;

    public GetApplicationsQueryHandler(IRepository<ApplicationEntity> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<GetApplicationsResult>> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var applications = await _repository.GetAllAsync(cancellationToken);

        return applications.Select(app => new GetApplicationsResult(
            app.Id,
            app.TenantId,
            app.Name,
            app.Description,
            app.ClientIdentifier,
            app.AllowedRedirectUris,
            app.AllowedOrigins,
            app.Status,
            app.CreatedAt,
            app.CreatedBy,
            app.ModifiedAt,
            app.ModifiedBy)).ToList();
    }
}
