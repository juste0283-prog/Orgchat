using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", table =>
        {
            // Une taille négative n'a aucun sens.
            table.HasCheckConstraint("CK_Attachments_SizeInBytes", "\"SizeInBytes\" >= 0");
        });

        builder.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.StorageFileName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);

        // Nom de stockage unique : un fichier physique = une seule ligne.
        builder.HasIndex(a => a.StorageFileName).IsUnique();

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
