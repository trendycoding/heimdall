using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class UserPermissionTemplateApplicationConfiguration : IEntityTypeConfiguration<UserPermissionTemplateApplication>
{
    public void Configure(EntityTypeBuilder<UserPermissionTemplateApplication> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AppliedAt)
            .IsRequired();

        builder.Property(e => e.AppliedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.SourceIp)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite index for lookups by user and template
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.UserProfileId, e.PermissionTemplateId });
    }
}
