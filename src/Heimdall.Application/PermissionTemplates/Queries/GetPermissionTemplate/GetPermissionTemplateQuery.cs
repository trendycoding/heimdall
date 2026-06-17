using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplate;

public sealed record GetPermissionTemplateQuery(Guid Id) : IRequest<PermissionTemplateDto?>;

public sealed record PermissionTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string TemplateCode,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy,
    IReadOnlyList<PermissionTemplatePermissionDto> Permissions,
    IReadOnlyList<PermissionTemplateGroupDto> Groups,
    IReadOnlyList<PermissionTemplateAccessDetailDto> AccessDetails);

public sealed record PermissionTemplatePermissionDto(
    Guid Id,
    Guid PermissionId,
    Effect Effect,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays);

public sealed record PermissionTemplateGroupDto(
    Guid Id,
    Guid GroupId);

public sealed record PermissionTemplateAccessDetailDto(
    Guid Id,
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays);
