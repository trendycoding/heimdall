using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class UserPermissionAssignmentConfiguration : IEntityTypeConfiguration<UserPermissionAssignment>
{
    public void Configure(EntityTypeBuilder<UserPermissionAssignment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Effect)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite lookup index: TenantId, ApplicationId, UserProfileId, PermissionId
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.UserProfileId, e.PermissionId });
    }
}
