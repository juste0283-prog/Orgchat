using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;

namespace OrgChat.Infrastructure.Configurations;

public class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        builder.ToTable("Calls", table =>
        {
            // Statut limité aux valeurs de l'énumération CallStatus.
            table.HasCheckConstraint("CK_Calls_Status", "\"Status\" IN (1, 2, 3, 4, 5)");
            // Un appel ne peut pas se terminer avant d'avoir commencé.
            table.HasCheckConstraint("CK_Calls_EndedAt", "\"EndedAt\" IS NULL OR \"EndedAt\" >= \"StartedAt\"");
        });

        builder.Property(c => c.Status)
            .IsRequired()
            .HasDefaultValue(CallStatus.Ringing);

        builder.HasIndex(c => new { c.ConversationId, c.StartedAt });
        builder.HasIndex(c => c.InitiatorId);

        builder.HasOne(c => c.Conversation)
            .WithMany(x => x.Calls)
            .HasForeignKey(c => c.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Initiator)
            .WithMany(u => u.InitiatedCalls)
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
