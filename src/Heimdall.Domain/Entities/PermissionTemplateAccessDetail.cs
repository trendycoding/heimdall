namespace Heimdall.Domain.Entities;

public class PermissionTemplateAccessDetail : BaseEntity
{
    public Guid PermissionTemplateId { get; set; }
    public string AccessDetailType { get; set; } = string.Empty;
    public string AccessDetailCode { get; set; } = string.Empty;
    public string AccessDetailValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ValidFromOffsetDays { get; set; }
    public int? ValidToOffsetDays { get; set; }
}
