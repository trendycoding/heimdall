namespace Heimdall.Domain.Entities;

public class UserAccessDetail : ApplicationScopedEntity
{
    public Guid UserProfileId { get; set; }
    public string AccessDetailType { get; set; } = string.Empty;
    public string AccessDetailCode { get; set; } = string.Empty;
    public string AccessDetailValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}
