using OrgChat.Core.Enums;
using System;
using System.Collections.Generic;

namespace OrgChat.Core.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // --- Extension plateforme : organisation, profil, statut ---

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Fonction de l'utilisateur (ex. « Technicien informatique »).</summary>
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>Département principal (optionnel ; DepartmentMembers gère
    /// les affectations et prépare le multi-affectation).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    /// <summary>Suspension temporaire : le compte redevient actif après cette échéance.</summary>
    public DateTime? SuspendedUntil { get; set; }
    public string? SuspensionReason { get; set; }

    /// <summary>Le mot de passe doit être défini via un jeton d'activation
    /// avant la première connexion.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? ActivatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<DepartmentMember> DepartmentMembers { get; set; } = new List<DepartmentMember>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    /// <summary>Le compte est-il utilisable flat maintenant ?
    /// Gère l'expiration automatique de la suspension temporaire (sans écrire en base).</summary>
    public bool IsLoginPermitted(DateTime utcNow) =>
        IsActive
        && Status switch
        {
            AccountStatus.Active => true,
            AccountStatus.Suspended => SuspendedUntil != null && SuspendedUntil <= utcNow,
            _ => false,
        };

    public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Call> InitiatedCalls { get; set; } = new List<Call>();
}
