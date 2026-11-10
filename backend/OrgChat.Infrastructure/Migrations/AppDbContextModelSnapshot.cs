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
            b.HasIndex("StorageFileName")
                .IsUnique();
            b.ToTable("Attachments", t =>
                {
                    t.HasCheckConstraint("CK_Attachments_SizeInBytes", "\"SizeInBytes\" >= 0");
                });

<<<<<<< HEAD
            b.HasOne("OrgChat.Core.Entities.Message", "Message")
                .WithMany("Attachments")
                .HasForeignKey("MessageId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("OrgChat.Core.Entities.Call", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            b.Property<int>("ConversationId")
                .HasColumnType("INTEGER");
            b.Property<int>("InitiatorId")
                .HasColumnType("INTEGER");
            b.Property<DateTime>("StartedAt")
                .HasColumnType("TEXT");
            b.Property<DateTime?>("EndedAt")
                .HasColumnType("TEXT");
            b.Property<byte>("Status")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue((byte)1);
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("InitiatorId");
            b.HasIndex(new[] { "ConversationId", "StartedAt" });
            b.ToTable("Calls", t =>
=======
            modelBuilder.Entity("OrgChat.Core.Entities.AuditLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int?>("ActorUserId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Details")
                        .HasMaxLength(2000)
                        .HasColumnType("TEXT");

                    b.Property<string>("EntityId")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<string>("Result")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("TEXT");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Action");

                    b.HasIndex("ActorUserId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("EntityType", "EntityId");

                    b.ToTable("AuditLogs");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Conversation", b =>
>>>>>>> develop
                {
                    t.HasCheckConstraint("CK_Calls_Status", "\"Status\" IN (1, 2, 3, 4, 5)");
                    t.HasCheckConstraint("CK_Calls_EndedAt", "\"EndedAt\" IS NULL OR \"EndedAt\" >= \"StartedAt\"");
                });

            b.HasOne("OrgChat.Core.Entities.Conversation", "Conversation")
                .WithMany("Calls")
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne("OrgChat.Core.Entities.User", "Initiator")
                .WithMany("InitiatedCalls")
                .HasForeignKey("InitiatorId")
                .OnDelete(DeleteBehavior.Restrict)
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
            b.ToTable("Conversations", t =>
                {
                    t.HasCheckConstraint("CK_Conversations_Type", "\"Type\" IN (1, 2)");
                });

<<<<<<< HEAD
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
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue(false);
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
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
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue(false);
            b.Property<DateTime?>("DeletedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
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
            b.Property<string?>("Payload")
                .HasColumnType("TEXT");
            b.Property<DateTime?>("ReadAt")
                .HasColumnType("TEXT");
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex(new[] { "UserId", "ReadAt" });
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
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue((byte)1);
            b.Property<bool>("IsActive")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue(true);
            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");
            b.HasIndex("Username", isUnique: true);
            b.HasIndex("Email", isUnique: true);
            b.ToTable("Users", t =>
=======
            modelBuilder.Entity("OrgChat.Core.Entities.Department", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsActive")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ManagerUserId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("OrganizationId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ManagerUserId");

                    b.HasIndex("OrganizationId");

                    b.HasIndex("OrganizationId", "Name")
                        .IsUnique();

                    b.ToTable("Departments");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.DepartmentMember", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int>("DepartmentId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsPrimary")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("JoinedAt")
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.HasIndex("DepartmentId", "UserId")
                        .IsUnique();

                    b.HasIndex("UserId", "IsPrimary");

                    b.ToTable("DepartmentMembers");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Invitation", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int>("CreatedByUserId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<byte>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("UsedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("UsedByIp")
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("ExpiresAt");

                    b.HasIndex("TokenHash")
                        .IsUnique();

                    b.HasIndex("UserId");

                    b.ToTable("Invitations");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Message", b =>
>>>>>>> develop
                {
                    t.HasCheckConstraint("CK_Users_Role", "\"Role\" IN (1, 2)");
                });
<<<<<<< HEAD
        });
=======

            modelBuilder.Entity("OrgChat.Core.Entities.Notification", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("ReadAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Notifications", (string)null);
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Organization", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Address")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsActive")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LogoPath")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("Phone")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("SettingsJson")
                        .HasMaxLength(8000)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Organizations");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            IsActive = true,
                            Name = "Organisation principale"
                        });
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Role", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsSystem")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("Roles");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Description = "Administration globale de la plateforme.",
                            IsSystem = true,
                            Key = "SuperAdmin",
                            Name = "Super administrateur"
                        },
                        new
                        {
                            Id = 2,
                            Description = "Gestion des utilisateurs, départements et paramètres de son organisation.",
                            IsSystem = true,
                            Key = "OrgAdmin",
                            Name = "Administrateur d'organisation"
                        },
                        new
                        {
                            Id = 3,
                            Description = "Tableau de bord de supervision, statistiques et rapports (métadonnées uniquement).",
                            IsSystem = true,
                            Key = "Supervisor",
                            Name = "Superviseur"
                        },
                        new
                        {
                            Id = 4,
                            Description = "Gestion des membres et suivi de l'activité de son département.",
                            IsSystem = true,
                            Key = "DepartmentManager",
                            Name = "Responsable de département"
                        },
                        new
                        {
                            Id = 5,
                            Description = "Accès à la messagerie et à son profil.",
                            IsSystem = true,
                            Key = "User",
                            Name = "Utilisateur"
                        });
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Session", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastSeenAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("RevokedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("TokenId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ExpiresAt");

                    b.HasIndex("TokenId")
                        .IsUnique();

                    b.HasIndex("UserId");

                    b.ToTable("Sessions");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("ActivatedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int?>("DepartmentId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsActive")
                        .HasColumnType("INTEGER");

                    b.Property<string>("JobTitle")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastLoginAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<bool>("MustChangePassword")
                        .HasColumnType("INTEGER");

                    b.Property<int>("OrganizationId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<byte>("Status")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("SuspendedUntil")
                        .HasColumnType("TEXT");

                    b.Property<string>("SuspensionReason")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("DepartmentId");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.HasIndex("OrganizationId");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("Users", (string)null);
                });

            modelBuilder.Entity("OrgChat.Core.Entities.UserActivity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Details")
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Type");

                    b.HasIndex("UserId", "CreatedAt");

                    b.ToTable("UserActivities");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.UserRole", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int?>("OrganizationId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("RoleId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("OrganizationId");

                    b.HasIndex("RoleId");

                    b.HasIndex("UserId");

                    b.HasIndex("UserId", "RoleId", "OrganizationId")
                        .IsUnique();

                    b.ToTable("UserRoles");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Attachment", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.Message", "Message")
                        .WithMany("Attachments")
                        .HasForeignKey("MessageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Message");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.AuditLog", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "Actor")
                        .WithMany()
                        .HasForeignKey("ActorUserId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Actor");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Conversation", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "CreatedBy")
                        .WithMany()
                        .HasForeignKey("CreatedById")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("CreatedBy");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.ConversationParticipant", b =>
                {
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

                    b.Navigation("Conversation");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Department", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "Manager")
                        .WithMany()
                        .HasForeignKey("ManagerUserId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("OrgChat.Core.Entities.Organization", "Organization")
                        .WithMany("Departments")
                        .HasForeignKey("OrganizationId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Manager");

                    b.Navigation("Organization");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.DepartmentMember", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.Department", "Department")
                        .WithMany("Members")
                        .HasForeignKey("DepartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany("DepartmentMembers")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Department");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Invitation", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "CreatedBy")
                        .WithMany()
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CreatedBy");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Message", b =>
                {
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

                    b.Navigation("Conversation");

                    b.Navigation("Sender");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Notification", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Session", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany("Sessions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.User", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.Department", "Department")
                        .WithMany()
                        .HasForeignKey("DepartmentId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("OrgChat.Core.Entities.Organization", "Organization")
                        .WithMany("Users")
                        .HasForeignKey("OrganizationId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Department");

                    b.Navigation("Organization");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.UserActivity", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.UserRole", b =>
                {
                    b.HasOne("OrgChat.Core.Entities.Organization", "Organization")
                        .WithMany()
                        .HasForeignKey("OrganizationId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("OrgChat.Core.Entities.Role", "Role")
                        .WithMany("UserRoles")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("OrgChat.Core.Entities.User", "User")
                        .WithMany("UserRoles")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Organization");

                    b.Navigation("Role");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Conversation", b =>
                {
                    b.Navigation("Messages");

                    b.Navigation("Participants");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Department", b =>
                {
                    b.Navigation("Members");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Message", b =>
                {
                    b.Navigation("Attachments");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Organization", b =>
                {
                    b.Navigation("Departments");

                    b.Navigation("Users");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.Role", b =>
                {
                    b.Navigation("UserRoles");
                });

            modelBuilder.Entity("OrgChat.Core.Entities.User", b =>
                {
                    b.Navigation("ConversationParticipants");

                    b.Navigation("DepartmentMembers");

                    b.Navigation("SentMessages");

                    b.Navigation("Sessions");

                    b.Navigation("UserRoles");
                });
#pragma warning restore 612, 618
        }
>>>>>>> develop
    }
}
