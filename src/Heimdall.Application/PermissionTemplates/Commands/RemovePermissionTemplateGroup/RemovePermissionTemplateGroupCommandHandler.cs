using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateGroup;

public sealed class RemovePermissionTemplateGroupCommandHandler
    : IRequestHandler<RemovePermissionTemplateGroupCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public RemovePermissionTemplateGroupCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(RemovePermissionTemplateGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PermissionTemplateGroups
            .FirstOrDefaultAsync(ptg => ptg.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionTemplateGroup '{request.Id}' was not found.");
        }

        _dbContext.PermissionTemplateGroups.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
