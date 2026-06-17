using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class ApiCallLogConfiguration : IEntityTypeConfiguration<ApiCallLog>
{
    public void Configure(EntityTypeBuilder<ApiCallLog> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RequestId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.Endpoint)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RequestPath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.CallerSubjectId)
            .HasMaxLength(256);

        builder.Property(e => e.CallerClientId)
            .HasMaxLength(256);

        builder.Property(e => e.SourceIp)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.StatusCode)
            .IsRequired();

        builder.Property(e => e.DurationMs)
            .IsRequired();

        builder.Property(e => e.RequestTimestamp)
            .IsRequired();

        builder.Property(e => e.ResponseTimestamp)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite lookup indexes
        builder.HasIndex(e => new { e.TenantId, e.CorrelationId });
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => new { e.TenantId, e.SourceIp, e.RequestTimestamp });
    }
}
