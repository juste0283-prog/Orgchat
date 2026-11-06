using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.Property(s => s.TokenId).IsRequired().HasMaxLength(64);
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(256);
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.LastSeenAt).IsRequired();

        builder.HasIndex(s => s.TokenId).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.ExpiresAt);

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
