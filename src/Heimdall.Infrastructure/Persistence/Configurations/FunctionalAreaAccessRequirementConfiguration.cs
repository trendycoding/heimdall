using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class FunctionalAreaAccessRequirementConfiguration : IEntityTypeConfiguration<FunctionalAreaAccessRequirement>
{
    public void Configure(EntityTypeBuilder<FunctionalAreaAccessRequirement> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AccessDetailType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsRequired)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Composite unique index: FunctionalAreaId, AccessDetailType
        builder.HasIndex(e => new { e.FunctionalAreaId, e.AccessDetailType })
            .IsUnique();
    }
}
