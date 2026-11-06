using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // --- Plateforme : organisations, RBAC, comptes, supervision ---

    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<DepartmentMember> DepartmentMembers { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<UserActivity> UserActivities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
