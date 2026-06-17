using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class GroupAccessDetailConfiguration : IEntityTypeConfiguration<GroupAccessDetail>
{
    public void Configure(EntityTypeBuilder<GroupAccessDetail> builder)
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

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite unique index: GroupId, ApplicationId, AccessDetailType, AccessDetailCode
        builder.HasIndex(e => new { e.GroupId, e.ApplicationId, e.AccessDetailType, e.AccessDetailCode })
            .IsUnique();

        // Composite lookup index: TenantId, ApplicationId, GroupId, AccessDetailType
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.GroupId, e.AccessDetailType });
    }
}
