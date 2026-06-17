namespace Heimdall.Domain.Entities;

public abstract class TenantScopedEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
