using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplatePermission;

public sealed class RemovePermissionTemplatePermissionCommandHandler
    : IRequestHandler<RemovePermissionTemplatePermissionCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public RemovePermissionTemplatePermissionCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(RemovePermissionTemplatePermissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PermissionTemplatePermissions
            .FirstOrDefaultAsync(ptp => ptp.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionTemplatePermission '{request.Id}' was not found.");
        }

        _dbContext.PermissionTemplatePermissions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
