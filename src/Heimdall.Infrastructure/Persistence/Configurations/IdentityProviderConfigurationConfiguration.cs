using System.Text.Json;
using Heimdall.Domain.Entities;
using Heimdall.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Infrastructure.Persistence.Configurations;

public class IdentityProviderConfigurationConfiguration : IEntityTypeConfiguration<IdentityProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<IdentityProviderConfiguration> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProviderType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Issuer)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.Audience)
            .HasMaxLength(500);

        builder.Property(e => e.ClientId)
            .HasMaxLength(256);

        builder.Property(e => e.JwksEndpoint)
            .HasMaxLength(1000);

        builder.Property(e => e.SamlMetadataUrl)
            .HasMaxLength(1000);

        builder.Property(e => e.ClockSkewToleranceSeconds)
            .IsRequired();

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

        // JSON conversion for AllowedAlgorithms (List<string>)
        builder.Property(e => e.AllowedAlgorithms)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("nvarchar(max)")
            .Metadata.SetValueComparer(stringListComparer);

        // ClaimMappings as owned JSON collection
        builder.OwnsMany(e => e.ClaimMappings, cm =>
        {
            cm.ToJson();
        });

        // Composite unique index: TenantId, ApplicationId, ProviderType, Name
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.ProviderType, e.Name })
            .IsUnique();
    }
}
