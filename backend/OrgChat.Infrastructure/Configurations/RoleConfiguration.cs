using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrgChat.Core.Entities;
using OrgChat.Core.Constants;

namespace OrgChat.Infrastructure.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(r => r.Key).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsSystem).IsRequired();

        builder.HasIndex(r => r.Key).IsUnique();

        builder.HasData(
            new Role { Id = 1, Key = RoleKeys.SuperAdmin, Name = "Super administrateur",
                Description = "Administration globale de la plateforme.", IsSystem = true },
            new Role { Id = 2, Key = RoleKeys.OrgAdmin, Name = "Administrateur d'organisation",
                Description = "Gestion des utilisateurs, départements et paramètres de son organisation.", IsSystem = true },
            new Role { Id = 3, Key = RoleKeys.Supervisor, Name = "Superviseur",
                Description = "Tableau de bord de supervision, statistiques et rapports (métadonnées uniquement).", IsSystem = true },
            new Role { Id = 4, Key = RoleKeys.DepartmentManager, Name = "Responsable de département",
                Description = "Gestion des membres et suivi de l'activité de son département.", IsSystem = true },
            new Role { Id = 5, Key = RoleKeys.User, Name = "Utilisateur",
                Description = "Accès à la messagerie et à son profil.", IsSystem = true });
    }
}
