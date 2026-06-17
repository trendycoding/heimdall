using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public PrimaryIdentityMode PrimaryIdentityMode { get; set; }
    public TenantStatus Status { get; set; }
}
