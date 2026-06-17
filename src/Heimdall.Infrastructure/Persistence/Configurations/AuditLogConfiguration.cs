using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ActorSubjectId)
            .HasMaxLength(256);

        builder.Property(e => e.ActorEmail)
            .HasMaxLength(320);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.EntityId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.BeforeJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.AfterJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.ChangedFieldsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.SourceIp)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite lookup indexes
        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
        builder.HasIndex(e => new { e.TenantId, e.CorrelationId });
        builder.HasIndex(e => new { e.TenantId, e.ApiCallLogId });
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
    }
}
