using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class PermissionTemplateAccessDetailConfiguration : IEntityTypeConfiguration<PermissionTemplateAccessDetail>
{
    public void Configure(EntityTypeBuilder<PermissionTemplateAccessDetail> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AccessDetailType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.AccessDetailCode)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.AccessDetailValue)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite unique index: PermissionTemplateId, AccessDetailType, AccessDetailCode
        builder.HasIndex(e => new { e.PermissionTemplateId, e.AccessDetailType, e.AccessDetailCode })
            .IsUnique();
    }
}
