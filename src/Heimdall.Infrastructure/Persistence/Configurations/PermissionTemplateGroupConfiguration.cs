using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class PermissionTemplateGroupConfiguration : IEntityTypeConfiguration<PermissionTemplateGroup>
{
    public void Configure(EntityTypeBuilder<PermissionTemplateGroup> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite unique index: PermissionTemplateId, GroupId
        builder.HasIndex(e => new { e.PermissionTemplateId, e.GroupId })
            .IsUnique();
    }
}
