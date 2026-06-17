using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class FunctionalAreaConfiguration : IEntityTypeConfiguration<FunctionalArea>
{
    public void Configure(EntityTypeBuilder<FunctionalArea> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FunctionalAreaCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Unique index: FunctionalAreaCode per application
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.FunctionalAreaCode })
            .IsUnique();
    }
}
