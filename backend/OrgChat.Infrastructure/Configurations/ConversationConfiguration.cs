using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", table =>
        {
            // 1 = Private, 2 = Group.
            table.HasCheckConstraint("CK_Conversations_Type", "\"Type\" IN (1, 2)");
        });

        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.Type).IsRequired();

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
