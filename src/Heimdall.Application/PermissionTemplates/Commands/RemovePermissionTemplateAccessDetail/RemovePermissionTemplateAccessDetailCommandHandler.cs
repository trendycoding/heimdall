using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateAccessDetail;

public sealed class RemovePermissionTemplateAccessDetailCommandHandler
    : IRequestHandler<RemovePermissionTemplateAccessDetailCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public RemovePermissionTemplateAccessDetailCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(RemovePermissionTemplateAccessDetailCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PermissionTemplateAccessDetails
            .FirstOrDefaultAsync(ptad => ptad.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionTemplateAccessDetail '{request.Id}' was not found.");
        }

        _dbContext.PermissionTemplateAccessDetails.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
