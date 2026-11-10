using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
        builder.Property(o => o.LogoPath).HasMaxLength(500);
        builder.Property(o => o.Email).HasMaxLength(256);
        builder.Property(o => o.Phone).HasMaxLength(50);
        builder.Property(o => o.Address).HasMaxLength(500);
        builder.Property(o => o.SettingsJson).HasMaxLength(8000);

        builder.HasIndex(o => o.Name).IsUnique();

        // Organisation par défaut : sert d'amorce (tests/intégration) et
        // cible de migration des utilisateurs existants.
        builder.HasData(new Organization
        {
            Id = 1,
            Name = "Organisation principale",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
