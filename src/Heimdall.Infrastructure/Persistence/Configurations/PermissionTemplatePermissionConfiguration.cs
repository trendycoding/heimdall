using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class PermissionTemplatePermissionConfiguration : IEntityTypeConfiguration<PermissionTemplatePermission>
{
    public void Configure(EntityTypeBuilder<PermissionTemplatePermission> builder)
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

        // Composite unique index: PermissionTemplateId, PermissionId
        builder.HasIndex(e => new { e.PermissionTemplateId, e.PermissionId })
            .IsUnique();
    }
}
