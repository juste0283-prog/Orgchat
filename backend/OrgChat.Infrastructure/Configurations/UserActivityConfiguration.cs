using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Configurations;

public class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.Property(a => a.Type).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Details).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(256);

        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => a.Type);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
