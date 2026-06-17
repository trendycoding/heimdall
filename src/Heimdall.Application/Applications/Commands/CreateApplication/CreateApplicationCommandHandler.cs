using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Applications.Commands.CreateApplication;

public sealed class CreateApplicationCommandHandler : IRequestHandler<CreateApplicationCommand, CreateApplicationResult>
{
    private readonly IRepository<ApplicationEntity> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateApplicationCommandHandler(
        IRepository<ApplicationEntity> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateApplicationResult> Handle(CreateApplicationCommand request, CancellationToken cancellationToken)
    {
        // Enforce ClientIdentifier uniqueness within the tenant
        var clientIdentifierExists = await _dbContext.Applications
            .AnyAsync(a => a.TenantId == _tenantContext.TenantId
                        && a.ClientIdentifier == request.ClientIdentifier, cancellationToken);

        if (clientIdentifierExists)
        {
            throw new InvalidOperationException(
                $"An application with ClientIdentifier '{request.ClientIdentifier}' already exists in this tenant.");
        }

        var application = new ApplicationEntity
        {
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            Description = request.Description,
            ClientIdentifier = request.ClientIdentifier,
            AllowedRedirectUris = request.AllowedRedirectUris ?? new List<string>(),
            AllowedOrigins = request.AllowedOrigins ?? new List<string>(),
            Status = ApplicationStatus.Active
        };

        var created = await _repository.AddAsync(application, cancellationToken);

        return new CreateApplicationResult(
            created.Id,
            created.TenantId,
            created.Name,
            created.Description,
            created.ClientIdentifier,
            created.AllowedRedirectUris,
            created.AllowedOrigins,
            created.Status,
            created.CreatedAt,
            created.CreatedBy);
    }
}
