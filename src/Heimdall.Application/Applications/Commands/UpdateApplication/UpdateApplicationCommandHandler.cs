using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Applications.Commands.UpdateApplication;

public sealed class UpdateApplicationCommandHandler : IRequestHandler<UpdateApplicationCommand, UpdateApplicationResult>
{
    private readonly IRepository<ApplicationEntity> _repository;

    public UpdateApplicationCommandHandler(IRepository<ApplicationEntity> repository)
    {
        _repository = repository;
    }

    public async Task<UpdateApplicationResult> Handle(UpdateApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (application is null)
        {
            throw new KeyNotFoundException($"Application with ID '{request.ApplicationId}' was not found.");
        }

        // Reject if application is Inactive
        if (application.Status == ApplicationStatus.Inactive)
        {
            throw new InvalidOperationException("Cannot update an inactive application.");
        }

        // Update only mutable fields: Name, Description, AllowedRedirectUris, AllowedOrigins
        // ClientIdentifier and Status are NOT modifiable through update
        application.Name = request.Name;
        application.Description = request.Description;
        application.AllowedRedirectUris = request.AllowedRedirectUris ?? new List<string>();
        application.AllowedOrigins = request.AllowedOrigins ?? new List<string>();

        await _repository.UpdateAsync(application, cancellationToken);

        return new UpdateApplicationResult(
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
