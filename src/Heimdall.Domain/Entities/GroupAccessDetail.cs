namespace Heimdall.Domain.Entities;

public class GroupAccessDetail : ApplicationScopedEntity
{
    public Guid GroupId { get; set; }
    public string AccessDetailType { get; set; } = string.Empty;
    public string AccessDetailCode { get; set; } = string.Empty;
    public string AccessDetailValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}
