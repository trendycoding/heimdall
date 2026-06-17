namespace Heimdall.Domain.Entities;

public abstract class ApplicationScopedEntity : TenantScopedEntity
{
    public Guid ApplicationId { get; set; }
}
