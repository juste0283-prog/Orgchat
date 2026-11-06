using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;
using OrgChat.Core.Constants;

namespace OrgChat.Infrastructure.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasIndex(d => new { d.OrganizationId, d.Name }).IsUnique();
        builder.HasIndex(d => d.OrganizationId);

        builder.HasOne(d => d.Organization)
            .WithMany(o => o.Departments)
            .HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
