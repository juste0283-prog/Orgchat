#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;

namespace OrgChat.Infrastructure.Migrations;

[DbContext(typeof(OrgChat.Infrastructure.Data.AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("OrgChat.Core.Entities.Attachment", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<string>("ContentType")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT(100)");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");
            b.Property<long>("SizeInBytes")
                .HasColumnType("INTEGER");
            b.Property<string>("StorageFileName")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT(200)");
            b.Property<string>("StoragePath")
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnType("TEXT(1000)");
            b.Property<string>("OriginalFileName")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("TEXT(500)");
            b.Property<int>("MessageId")
                .HasColumnType("INTEGER");

            b.HasKey("Id");
            b.HasIndex("MessageId");
            b.ToTable("Attachments");

            b.HasOne("OrgChat.Core.Entities.Message", "Message")
                .WithMany("Attachments")
                .HasForeignKey("MessageId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.Conversation", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<string?>("Title")
                .HasMaxLength(200)
                .HasColumnType("TEXT(200)");
            b.Property<byte>("Type")
                .HasColumnType("INTEGER");
            b.Property<int>("CreatedById")
                .HasColumnType("INTEGER");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("CreatedById");
            b.ToTable("Conversations");

            b.HasOne("OrgChat.Core.Entities.User", "CreatedBy")
                .WithMany()
                .HasForeignKey("CreatedById")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.ConversationParticipant", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<int>("ConversationId")
                .HasColumnType("INTEGER");
            b.Property<int>("UserId")
                .HasColumnType("INTEGER");
            b.Property<DateTime>("JoinedAt")
                .HasColumnType("TEXT");
            b.Property<DateTime?>("LastReadAt")
                .HasColumnType("TEXT");
            b.Property<bool>("IsGroupAdmin")
                .HasColumnType("INTEGER");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("ConversationId");
            b.HasIndex("UserId");
            b.HasIndex(new[] { "ConversationId", "UserId" }, isUnique: true);
            b.ToTable("ConversationParticipants");

            b.HasOne("OrgChat.Core.Entities.Conversation", "Conversation")
                .WithMany("Participants")
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne("OrgChat.Core.Entities.User", "User")
                .WithMany("ConversationParticipants")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.Message", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<int>("ConversationId")
                .HasColumnType("INTEGER");
            b.Property<int>("SenderId")
                .HasColumnType("INTEGER");
            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");
            b.Property<DateTime?>("EditedAt")
                .HasColumnType("TEXT");
            b.Property<bool>("IsDeleted")
                .HasColumnType("INTEGER");
            b.Property<DateTime?>("DeletedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("ConversationId");
            b.HasIndex("SenderId");
            b.HasIndex(new[] { "ConversationId", "CreatedAt" });
            b.ToTable("Messages");

            b.HasOne("OrgChat.Core.Entities.Conversation", "Conversation")
                .WithMany("Messages")
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne("OrgChat.Core.Entities.User", "Sender")
                .WithMany("SentMessages")
                .HasForeignKey("SenderId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.Notification", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<int>("UserId")
                .HasColumnType("INTEGER");
            b.Property<string>("Type")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT(100)");
            b.Property<string>("Payload")
                .IsRequired()
                .HasColumnType("TEXT");
            b.Property<DateTime?>("ReadAt")
                .HasColumnType("TEXT");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("UserId");
            b.ToTable("Notifications");

            b.HasOne("OrgChat.Core.Entities.User", "User")
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.User", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<string>("Username")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT(100)");
            b.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("TEXT(256)");
            b.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT(100)");
            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("TEXT");
            b.Property<byte>("Role")
                .HasColumnType("INTEGER");
            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("Username", isUnique: true);
            b.HasIndex("Email", isUnique: true);
            b.ToTable("Users");
        });
    }
}
