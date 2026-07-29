using System.Text.Json;
using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class ApiKeyRegistrationConfiguration : IEntityTypeConfiguration<ApiKeyRegistration>
{
    public void Configure(EntityTypeBuilder<ApiKeyRegistration> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.KeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.KeyPrefix)
            .IsRequired()
            .HasMaxLength(12);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        builder.Property(e => e.Scopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("nvarchar(max)")
            .Metadata.SetValueComparer(stringListComparer);

        // Index on KeyHash for fast lookup
        builder.HasIndex(e => e.KeyHash).IsUnique();

        // Index on prefix for fast prefix-based search
        builder.HasIndex(e => e.KeyPrefix);

        // Index on tenant for listing
        builder.HasIndex(e => e.TenantId);
    }
}
