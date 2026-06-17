using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Applications.Commands.DeactivateApplication;

public sealed class DeactivateApplicationCommandHandler : IRequestHandler<DeactivateApplicationCommand, DeactivateApplicationResult>
{
    private readonly IRepository<ApplicationEntity> _repository;

    public DeactivateApplicationCommandHandler(IRepository<ApplicationEntity> repository)
    {
        _repository = repository;
    }

    public async Task<DeactivateApplicationResult> Handle(DeactivateApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (application is null)
        {
            throw new KeyNotFoundException($"Application with ID '{request.ApplicationId}' was not found.");
        }

        // Reject if already inactive
        if (application.Status == ApplicationStatus.Inactive)
        {
            throw new InvalidOperationException("Application is already inactive.");
        }

        // Populate cache invalidation context
        request.TenantId = application.TenantId;

        application.Status = ApplicationStatus.Inactive;

        await _repository.UpdateAsync(application, cancellationToken);

        return new DeactivateApplicationResult(
            application.Id,
            application.TenantId,
            application.Name,
            application.Status,
            application.ModifiedAt,
            application.ModifiedBy);
    }
}
