# ORGCHAT — Documentation Technique Backend

**Projet :** OrgChat — Application de messagerie LAN/VPN académique  
**Stack :** .NET 10, ASP.NET Core Web API, SignalR, Entity Framework Core, SQLite  
**Date :** Novembre 2026

---

## 1. Architecture et structure de la solution

### 1.1 Projets

| Projet | Rôle | Technologies |
|--------|------|--------------|
| `OrgChat.Core` | Entités du domaine, enums, contrats (DTOs) | .NET 10 (class library) |
| `OrgChat.Infrastructure` | `DbContext`, configurations EF Core, migrations, services | EF Core 10, SQLite |
| `OrgChat.Server` | API ASP.NET Core, contrôleurs, hub SignalR, configuration auth | ASP.NET Core 10, JWT Bearer, SignalR |
| `OrgChat.Tests` | Tests unitaires et d'intégration | xUnit, `Microsoft.AspNetCore.Mvc.Testing` |

### 1.2 Schéma des couches

```
Client (WinForms, séparé)
    │ HTTP API + SignalR
    ▼
┌─────────────────────────────────────┐
│  OrgChat.Server (ASP.NET Core)      │
│  ├── Controllers (API REST)         │
│  ├── Hubs (ChatHub SignalR)         │
│  ├── Services (Conversation, Message)│
│  └── Program.cs (DI, Auth, Migration)│
└─────────────────────────────────────┘
    │ EF Core (Code-First)
    ▼
┌─────────────────────────────────────┐
│  OrgChat.Infrastructure             │
│  ├── AppDbContext                   │
│  ├── Configurations (Fluent API)    │
│  ├── Migrations                     │
│  └── Services (PasswordHasher, Token)│
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│  SQLite (OrgChat.db, serveur seulement)│
└─────────────────────────────────────┘
```

## 2. Modèle de données

### 2.1 Entités

| Entité | Table | Description |
|--------|-------|-------------|
| `User` | `Users` | Utilisateur : username, email (pro), display name, prénom/nom, fonction, hash de mot de passe, statut (`AccountStatus` : Active/Suspended/Disabled), suspension temporaire (`SuspendedUntil`, `SuspensionReason`), organisation, département principal, `MustChangePassword` (première connexion), `ActivatedAt`, `LastLoginAt` |
| `Conversation` | `Conversations` | Conversation : titre, type (privé/groupe), créateur, date |
| `ConversationParticipant` | `ConversationParticipants` | Lien utilisateur ↔ conversation : date de jointure, dernière lecture, admin de groupe |
| `Message` | `Messages` | Message : conversation, expéditeur, contenu, horodatage serveur, état édité/supprimé |
| `Attachment` | `Attachments` | Pièce jointe : message, nom original, nom de stockage, type, taille, chemin |
| `Notification` | `Notifications` | Notification : utilisateur, type, payload JSON, date de lecture |
| `Organization` | `Organizations` | Organisation : nom (unique), logo, coordonnées (email/phone/adresse), statut, paramètres JSON |
| `Department` | `Departments` | Département d'une organisation : nom (unique par organisation), description, responsable (`ManagerUserId`), statut |
| `DepartmentMember` | `DepartmentMembers` | Affectation utilisateur ↔ département avec `IsPrimary` (conçu pour évoluer vers le multi-affectation) |
| `Role` | `Roles` | Rôle système RBAC (sémé : SuperAdmin, OrgAdmin, Supervisor, DepartmentManager, User) |
| `UserRole` | `UserRoles` | Attribution d'un rôle à un utilisateur, éventuellement bornée à une organisation (`OrganizationId` null = rôle global) |
| `Invitation` | `Invitations` | Jeton d'activation ou de réinitialisation : hash SHA-256 (le jeton en clair n'est jamais stocké), expiration, usage unique |
| `Session` | `Sessions` | Session JWT persistée : `TokenId` (claim jti, unique), expiration, révocation, IP, user-agent |
| `AuditLog` | `AuditLogs` | Journal d'audit en écriture seule : acteur, action, entité, résultat, IP, user-agent — protégé par triggers SQLite |
| `UserActivity` | `UserActivities` | Trace d'activité (métadonnées uniquement) : connexions, activations — pour la supervision |

### 2.1.1 Rôles RBAC (sémés en base)

| Id | Clé | Libellé | Périmètre |
|----|-----|---------|-----------|
| 1 | `SuperAdmin` | Super administrateur | Plateforme globale, organisations |
| 2 | `OrgAdmin` | Administrateur d'organisation | Utilisateurs, départements, rôles, paramètres de son organisation |
| 3 | `Supervisor` | Superviseur | Supervision et statistiques (métadonnées uniquement, jamais le contenu des conversations) |
| 4 | `DepartmentManager` | Responsable de département | Membres et activité de son département |
| 5 | `User` | Utilisateur | Messagerie et profil |

Les clés sont les claims de rôle du JWT ; les permissions sont vérifiées côté serveur
(`[Authorize(Roles = "SuperAdmin,OrgAdmin")]` + règles métier) — le client ne fait que
masquer des menus.

### 2.2 Contraintes clés

- `Users.Username` — unique, indexé
- `Users.Email` — unique, indexé
- `ConversationParticipants(ConversationId, UserId)` — unique composite
- `Messages.ConversationId` — indexé
- `Messages(ConversationId, CreatedAt)` — index composé pour l'historique paginé
- Suppression en cascade : conversation → participants, messages, attachments ; message → attachments ; utilisateur → notifications
- Suppression restreinte : conversation → créateur ; message → expéditeur ; participant → utilisateur ; audit → acteur (le journal survit à la suppression des comptes)
- `Organizations.Name` — unique, indexé
- `Departments(OrganizationId, Name)` — unique composite
- `DepartmentMembers(DepartmentId, UserId)` — unique composite
- `Roles.Key` — unique, sémé
- `UserRoles(UserId, RoleId, OrganizationId)` — unique composite
- `Invitations.TokenHash` — unique, indexé ; `Sessions.TokenId` — unique, indexé
- `AuditLogs.CreatedAt` / `AuditLogs.Action` / `AuditLogs.ActorUserId` / `AuditLogs(EntityType, EntityId)` — indexés
- Triggers `AuditLogs_PreventUpdate` / `AuditLogs_PreventDelete` : toute tentative
  de modification ou de suppression directe d'une entrée d'audit en base est rejetée
  (`RAISE(ABORT)`).

### 2.3 Migration PlatformV2 (non destructive)

La migration `PlatformV2` ajoute les tables de la plateforme (Organizations,
Departments, DepartmentMembers, Roles, UserRoles, Invitations, Sessions,
AuditLogs, UserActivities) et les colonnes étendues de `Users` **sans détruire
les données** :

- les utilisateurs existants sont rattachés à l'organisation par défaut
  « Organisation principale » (Id 1) ;
- l'ancienne colonne `Users.Role` (1 = User, 2 = Admin) est convertie en
  lignes `UserRoles` (2 → OrgAdmin, sinon → Utilisateur) puis supprimée ;
- `Users.Status` est initialisé à `Active` (1).

**Sauvegarde** : le serveur copie automatiquement la base dans
`OrgChat.Server/backups/OrgChat_<horodatage>.db` avant chaque application de
migrations (`DatabaseBackup.CreateBeforeMigration`). Script manuel de
sauvegarde et de purge 30 jours : `backend/scripts/backup-database.ps1`.

### 2.4 Script PostgreSQL

Un script SQL équivalent est fourni dans `scripts/postgresql/init.sql` pour migration éventuelle vers PostgreSQL (nécessite le provider Npgsql EF Core).

---

## 3. Authentification et sécurité

### 3.1 Hachage des mots de passe

- Algorithme : **PBKDF2** avec HMAC-SHA256
- Iterations : 100 000
- Salt : 16 octets aléatoires par mot de passe
- Hash : 32 octets
- Stockage : Base64(salt + hash)
- Comparaison : à temps constant (protection contre les attaques par timing)
- Aucun package supplémentaire requis (utilise `System.Security.Cryptography.Rfc2898DeriveBytes`)

### 3.2 Jetons JWT

- Algorithme : HMAC-SHA256
- Claims : `sub` (ID utilisateur), `name` (username), `role` (rôle)
- Expiration : configurable via `JwtSettings:ExpirationMinutes` (défaut 120 min)
- ClockSkew : 0 (validation stricte de l'expiration)
- Issuer/Audience : `OrgChat`

### 3.3 Règles de sécurité

| Règle | Implémentation |
|-------|---------------|
| Pas de mot de passe en clair | PBKDF2 hash à l'inscription et vérification à la connexion |
| Compte désactivé bloqué | Vérification `IsActive` à la connexion **et à chaque requête authentifiée** (middleware `Program.cs`) |
| Révocation immédiate | Un JWT encore valide est rejeté en `401` si le compte a été désactivé, supprimé ou anonymisé |
| Mot de passe minimal | 8 caractères pour l'inscription, le changement personnel et la réinitialisation admin |
| Messages d'erreur génériques | "Identifiants invalides" (ne révèle pas si l'utilisateur existe) |
| Autorisation API | `[Authorize]` sur tous les endpoints sensibles ; `[Authorize(Roles = "Admin")]` pour l'administration |
| Autorisation SignalR | `[Authorize]` sur le `ChatHub` ; vérification d'appartenance à la conversation |
| Groupes SignalR | Un utilisateur retiré/désabonné est aussi retiré du groupe de conversation côté serveur |
| Horodatage serveur | `CreatedAt` défini par le serveur ; le client ne fournit jamais de date de message |
| Persistance avant diffusion | Message sauvegardé en base avant broadcast SignalR |
| Pièces jointes | Taille max 20 Mo, extensions exécutables bloquées, quota 500 Mo par conversation, `X-Content-Type-Options: nosniff` |
| Suppression de compte | Anonymisation logique (`username`, `email`, `displayName`, hash) pour préserver l'intégrité des messages |

## 4. API REST — Référence des endpoints

### 4.1 Authentification

| Méthode | Endpoint | Corps | Réponse | Description |
|---------|----------|-------|---------|-------------|
| `POST` | `/api/auth/register` | `RegisterRequest` | `AuthResponse` | Inscription libre — **désactivée par défaut** ; ne reste active que si `OrgChat:EnableSelfRegistration=true` (amorce de tests). En production, seuls les administrateurs créent les comptes. |
| `POST` | `/api/auth/login` | `LoginRequest` | `AuthResponse` | Connexion : vérifie le statut du compte (suspension, désactivation), crée une session persistée (jti), journalise l'audit et l'activité, retourne le JWT |
| `GET` | `/api/auth/activate?token=…` | — | Page HTML | Page d'activation autonome ouverte par le lien reçu ; elle permet de choisir le mot de passe et appelle le `POST` ci-dessous |
| `POST` | `/api/auth/activate` | `ActivateAccountRequest` | `AuthResponse` | Consomme le jeton à usage unique, définit le mot de passe, active le compte et ouvre une session |
| `POST` | `/api/auth/logout` | — | 204 | Révoque la session courante et journalise la déconnexion (authentifié) |

**Flux de connexion (login)** :
1. Vérification des identifiants (PBKDF2-SHA256, comparaison à temps constant).
2. Contrôle du statut : `IsActive` + `AccountStatus` (un compte `Suspended` avec
   `SuspendedUntil` expiré redevient automatiquement utilisable).
3. Création d'une entrée `Sessions` avec un `jti` unique intégré au JWT
   (expiration 120 min) — la session est révocable côté serveur.
4. Purge des sessions expirées de l'utilisateur, mise à jour de
   `Users.LastLoginAt`, écriture AuditLog (`LoggedIn`) + UserActivity (`Login`).

**AuthResponse :**
```json
{
  "token": "string (JWT)",
  "userId": 0,
  "username": "string",
  "email": "string",
  "displayName": "string",
  "role": "SuperAdmin | OrgAdmin | Supervisor | DepartmentManager | User"
}
```

Le JWT embarque : `sub` (Id utilisateur), `name` (username), `jti` (session),
`org` (Id d'organisation), autant de claims `role` que de rôles attribués.
À chaque requête, le middleware vérifie compte actif + statut + session non
révoquée/expirée.

### 4.2 Utilisateurs

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/users/me` | Authentifié | Profil de l'utilisateur connecté |
| `GET` | `/api/users` | Authentifié | Liste des utilisateurs du périmètre (infos publiques) |
| `GET` | `/api/users/{id}` | Authentifié | Profil d'un utilisateur du périmètre |
| `POST` | `/api/users` | SuperAdmin/OrgAdmin | Crée un compte : lien d'activation **ou** mot de passe direct (`CreateUserRequest`) |
| `PUT` | `/api/users/{id}` | Admin / soi-même | Modifie un profil |
| `PUT` | `/api/users/{id}/role` | SuperAdmin/OrgAdmin | Change le rôle ; `OrgAdmin`/`Supervisor` réservés au SuperAdmin |
| `PUT` | `/api/users/me/password` | Authentifié | Change son propre mot de passe |
| `PUT` | `/api/users/{id}/password` | Admin | Réinitialise le mot de passe |
| `PUT` | `/api/users/{id}/deactivate` | Admin | Désactive un compte |
| `PUT` | `/api/users/{id}/reactivate` | Admin | Réactive un compte |
| `POST` | `/api/users/{id}/suspend` | Admin | Suspension temporaire (durée ou date, motif) |
| `POST` | `/api/users/{id}/unsuspend` | Admin | Lève la suspension |
| `POST` | `/api/users/{id}/reset-password` | Admin | Réinitialise via jeton (sans jamais voir le mot de passe) |
| `POST` | `/api/users/{id}/resend-activation` | Admin | Réémet un lien d'activation (ancien jeton révoqué) |
| `GET` | `/api/users/me/sessions` | Authentifié | Sessions actives de l'utilisateur |
| `DELETE` | `/api/users/me/sessions/{sessionId}` | Authentifié | Révoque une de ses sessions |
| `GET` | `/api/users/{id}/sessions` | Admin | Sessions d'un compte |
| `DELETE` | `/api/users/{id}/sessions/{sessionId}` | Admin | Révoque une session d'un compte |
| `DELETE` | `/api/users/{id}/sessions` | Admin | Révoque toutes les sessions d'un compte |
| `GET` | `/api/users/me/activity` | Authentifié | Historique d'activité (connexions, etc.) |
| `GET` | `/api/users/{id}/activity` | Admin | Historique d'activité d'un compte |
| `POST` | `/api/users/import` | SuperAdmin/OrgAdmin | Import CSV de comptes |
| `DELETE` | `/api/users/{id}` | Admin | Supprime/anonymise un compte (sauf soi-même) |

**Règles d'administration :**
- Aucune auto-inscription : `POST /api/auth/register` est désactivé par défaut.
- Seul le SuperAdmin peut désigner un `OrgAdmin` ou un `Supervisor`.
- Un admin ne peut pas se retirer lui-même son propre rôle administrateur.
- Le dernier administrateur actif est protégé : impossible de le rétrograder, de le désactiver ou de le supprimer.
- Un `OrgAdmin` ne peut pas modifier un compte `OrgAdmin`, `Supervisor` ou `SuperAdmin`.
- La suppression est une anonymisation (`Compte supprimé`, identifiants remplacés) afin de conserver les messages et pièces jointes liés.
- Toute action d'administration est journalisée dans `AuditLogs` (acteur, action, entité, résultat, IP).

### 4.3 Parcours création → activation d'un compte

1. **L'administrateur crée le compte** depuis `Utilisateurs → Créer un compte`.
   Deux modes explicites sont proposés :
   - **Lien d'activation** (par défaut) : aucun mot de passe n'est saisi. Le
     compte est créé avec `MustChangePassword=true` et `ActivatedAt=null` ; il
     ne peut pas encore se connecter.
   - **Mot de passe immédiat** : l'administrateur saisit un mot de passe
     (8 caractères minimum). Le compte est actif immédiatement.
2. **Le serveur émet un jeton** pour le mode lien : 32 octets aléatoires,
   stocké uniquement sous forme de hash SHA-256 dans `Invitations`, avec
   expiration (72 h par défaut) et usage unique. La réponse `CreatedUserDto`
   contient `activationToken`, `activationLink` (URL absolue) et
   `invitationExpiresAt`.
3. **Le client affiche une fenêtre de confirmation** avec l'identifiant, le
   lien complet, la date d'expiration et un bouton **Copier le lien**.
   L'administrateur transmet le lien par e-mail ou messagerie interne.
4. **L'utilisateur ouvre le lien** dans son navigateur (page HTML
   `/api/auth/activate?token=…`) ou le colle dans le client OrgChat
   (`Première connexion ? Activer mon compte`). Il choisit son mot de passe.
5. **Le jeton est consommé**, le mot de passe est haché (PBKDF2-SHA256), le
   compte est activé (`ActivatedAt`, `MustChangePassword=false`) et une session
   est créée. Le jeton ne peut plus être réutilisé.
6. **Compte en attente** : tant que l'étape 4 n'est pas terminée, la liste des
   utilisateurs affiche une pastille **En attente**. Le menu **Gérer** propose
   alors **Obtenir un lien d'activation** : le serveur révoque l'ancien jeton
   (s'il existe) et en émet un nouveau, sans jamais afficher de mot de passe.

**Configuration du lien absolu :** le serveur utilise
`OrgChat:PublicBaseUrl` s'il est renseigné, sinon `Request.Scheme://Request.Host`.
En production derrière un reverse proxy, définir par exemple
`OrgChat__PublicBaseUrl=https://chat.example.org` pour que les liens envoyés
par e-mail soient accessibles depuis l'extérieur.

**UserDto :**
```json
{
  "id": 0,
  "username": "string",
  "email": "string",
  "displayName": "string",
  "firstName": "string",
  "lastName": "string",
  "jobTitle": "string",
  "departmentId": 0,
  "departmentName": "string",
  "organizationId": 0,
  "role": "SuperAdmin | OrgAdmin | Supervisor | DepartmentManager | User",
  "isActive": true,
  "status": "Active | Suspended | Disabled",
  "suspendedUntil": null,
  "suspensionReason": null,
  "mustChangePassword": false,
  "activatedAt": null,
  "lastLoginAt": null,
  "isOnline": false
}
```

### 4.3 Conversations

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/conversations` | Authentifié | Liste des conversations de l'utilisateur |
| `GET` | `/api/conversations/{id}` | Authentifié + participant | Détail d'une conversation |
| `POST` | `/api/conversations` | Authentifié | Crée une conversation privée ou groupe |
| `POST` | `/api/conversations/{id}/read` | Authentifié + participant | Marque la conversation comme lue et diffuse l'accusé `ConversationRead` |
| `POST` | `/api/conversations/{id}/participants` | Authentifié / admin groupe | Ajoute un participant et diffuse `ConversationUpdated` |
| `DELETE` | `/api/conversations/{id}/participants/{userId}` | Authentifié / admin groupe | Retire un participant, nettoie ses notifications et diffuse les événements |
| `POST` | `/api/conversations/{id}/leave` | Authentifié + participant | Quitte un groupe ; supprime la conversation s'il ne reste qu'une personne |
| `DELETE` | `/api/conversations/{id}` | Authentifié / admin groupe ou créateur | Supprime la conversation pour tous |

**ConversationCreateRequest :**
```json
{
  "title": "string (optionnel, requis pour groupe)",
  "type": "Private | Group",
  "participantUserIds": [0]
}
```

**Règles métier :**
- Privé : exactement 2 participants (créateur + 1 invité)
- Groupe : au moins 2 participants, titre requis
- Seuls les admins de groupe peuvent ajouter/retirer des participants
- Un groupe ou privé ne peut descendre en dessous de 2 participants via l'ajout/retrait ; `leave` supprime automatiquement un groupe qui ne garderait qu'un participant
- Si l'admin retiré ou partant était le dernier admin, un participant restant est promu automatiquement
- La suppression d'une conversation nettoie les notifications, les pièces jointes et les groupes SignalR
- Une conversation privée peut être supprimée par l'un de ses deux participants

**ReadReceiptDto :**
```json
{
  "conversationId": 0,
  "userId": 0,
  "readAt": "2026-11-05T19:00:00Z"
}
```

### 4.4 Messages

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/conversations/{id}/messages` | Authentifié + participant | Historique paginé (page, pageSize max 100) |
| `POST` | `/api/conversations/{id}/messages` | Authentifié + participant | Envoie un message |
| `PUT` | `/api/conversations/{id}/messages/{messageId}` | Auteur + participant | Modifie le contenu d'un message et diffuse `MessageUpdated` |
| `DELETE` | `/api/conversations/{id}/messages/{messageId}` | Auteur + participant | Supprime logiquement un message et diffuse `MessageDeleted` |
| `POST` | `/api/conversations/{id}/messages/attachments` | Authentifié + participant | Envoie un message avec pièce jointe (`multipart/form-data`) |
| `GET` | `/api/attachments/{id}` | Authentifié + participant | Télécharge une pièce jointe |

**MessageCreateRequest :**
```json
{
  "content": "string"
}
```

**MessageDto :**
```json
{
  "id": 0,
  "conversationId": 0,
  "senderId": 0,
  "senderUsername": "string",
  "content": "string",
  "createdAt": "2026-11-05T19:00:00Z",
  "editedAt": null,
  "isDeleted": false,
  "deletedAt": null,
  "attachments": [
    {
      "id": 0,
      "messageId": 0,
      "originalFileName": "string",
      "contentType": "string",
      "sizeInBytes": 0,
      "downloadUrl": "/api/attachments/0"
    }
  ]
}
```

**Règles de modification/suppression :**
- Seul l'auteur du message peut le modifier ou le supprimer, et il doit encore être participant de la conversation.
- La suppression est logique : `IsDeleted`, `DeletedAt`, contenu effacé ; le message reste en base pour l'intégrité mais n'est plus affiché.
- Les pièces jointes d'un message supprimé ne sont plus téléchargeables.
- Les notifications liées à un message supprimé sont supprimées.
- Un message modifié expose `EditedAt` et un indicateur « modifié » côté client.

**Pièces jointes :**
- Champ `file` obligatoire, champ `content` optionnel (légende).
- Taille maximale : 20 Mo ; extensions exécutables bloquées (`.exe`, `.bat`, `.ps1`, `.dll`, etc.).
- Quota de stockage : 500 Mo par conversation.
- Stockage hors web root dans `uploads/{année}/{mois}/{guid}.{ext}` sous le répertoire de contenu du serveur.
- Le téléchargement vérifie que l'utilisateur est participant de la conversation du message et que le message n'est pas supprimé.
- La réponse ajoute `X-Content-Type-Options: nosniff` et utilise `Content-Disposition` pour forcer le téléchargement.
- Le message et sa pièce jointe sont diffusés via `ReceiveMessage` comme un message classique.

### 4.6 Recherche

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/search/messages?q=...&take=50` | Authentifié | Recherche dans les messages des conversations de l'utilisateur |

La recherche exige au moins 2 caractères, ignore les messages supprimés et retourne pour chaque résultat le message, l'identifiant de conversation, son titre et le nom de l'expéditeur. Le client ouvre directement la conversation correspondante.

### 4.5 Notifications

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/notifications?take=100&skip=0` | Authentifié | Liste paginée des notifications de l'utilisateur (max 200 par page) |
| `GET` | `/api/notifications/unread-count` | Authentifié | Nombre de notifications non lues |
| `POST` | `/api/notifications/{id}/read` | Authentifié | Marque une notification comme lue |
| `POST` | `/api/notifications/read-all` | Authentifié | Marque toutes les notifications comme lues |
| `DELETE` | `/api/notifications/{id}` | Authentifié | Supprime une notification |
| `DELETE` | `/api/notifications` | Authentifié | Supprime toutes les notifications de l'utilisateur |
| `POST` | `/api/notifications/purge?days=30` | Authentifié | Purge les notifications lues plus anciennes que N jours |

**NotificationDto :**
```json
{
  "id": 0,
  "userId": 0,
  "type": "Message",
  "conversationId": 0,
  "messageId": 0,
  "senderId": 0,
  "senderName": "string",
  "conversationTitle": "string",
  "preview": "string",
  "createdAt": "2026-11-05T19:00:00Z",
  "readAt": null
}
```

Les notifications sont persistées dans la table `Notifications` existante, avec les détails du message sérialisés dans `Payload` (JSON). Elles sont créées après chaque message pour les participants autres que l'expéditeur. `POST /api/conversations/{id}/read` marque également comme lues les notifications liées à cette conversation.

### 4.6 Organisations et départements

**Organisations (`/api/organizations`)**

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/current` | Authentifié | Organisation courante (nom, logo, coordonnées, paramètres) |
| `GET` | `/` | SuperAdmin | Liste de toutes les organisations |
| `POST` | `/` | SuperAdmin | Crée une organisation |
| `PUT` | `/current` | SuperAdmin/OrgAdmin | Met à jour l'organisation courante |
| `PUT` | `/{id}/activate` | SuperAdmin | Active une organisation |
| `PUT` | `/{id}/deactivate` | SuperAdmin | Désactive une organisation |
| `PUT` | `/current/logo` | SuperAdmin/OrgAdmin | Téléverse le logo |
| `GET` | `/{id}/logo` | Authentifié | Récupère le logo |

**Départements (`/api/departments`)**

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/` | Authentifié | Départements du périmètre |
| `GET` | `/{id}` | Authentifié | Détail d'un département |
| `POST` | `/` | SuperAdmin/OrgAdmin | Crée un département |
| `PUT` | `/{id}` | SuperAdmin/OrgAdmin | Modifie un département |
| `DELETE` | `/{id}` | SuperAdmin/OrgAdmin | Désactive un département |
| `POST` | `/{id}/members` | SuperAdmin/OrgAdmin | Affecte un membre |
| `DELETE` | `/{id}/members/{userId}` | SuperAdmin/OrgAdmin | Retire un membre |
| `GET` | `/{id}/stats` | Responsable du département ou admin | Statistiques du département |

### 4.7 Supervision et audit

**Supervision (`/api/supervision`)** — rôles `SuperAdmin`, `OrgAdmin`, `Supervisor`.
Périmètre : organisation du JWT, sauf SuperAdmin (global). Métadonnées uniquement,
aucun contenu de conversation.

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/summary` | Compteurs globaux (comptes, présence, sessions, messages 24 h, connexions) |
| `GET` | `/users?search=&status=&role=&departmentId=&page=&pageSize=` | Comptes paginés |
| `GET` | `/departments` | Statistiques par département |
| `GET` | `/activity?days=30` | Série quotidienne (1 à 90 jours) |
| `GET` | `/connections?hours=24` | Sessions par heure (1 à 168 h) |
| `GET` | `/health` | Santé API / base / SignalR |
| `GET` | `/incidents?days=7&page=&pageSize=` | Échecs de connexion et résultats non `Success` |
| `GET` | `/reports?period=daily\|weekly\|monthly` | Rapport agrégé, top utilisateurs, départements |

**Audit (`/api/audit`)** — rôles `SuperAdmin`, `OrgAdmin`, `Supervisor`.

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/?search=&action=&entityType=&result=&actorUserId=&from=&to=&page=&pageSize=` | Journal paginé et filtrable |
| `GET` | `/facets` | Actions, types d'entité et résultats distincts |

Le journal est en écriture seule (triggers SQLite). Un non-SuperAdmin ne voit que
les entrées dont l'acteur appartient à son organisation.

---

## 5. SignalR — Hub de chat

### 5.1 Endpoint

- URL : `/hubs/chat`
- Authentification : JWT Bearer (token passé dans l'URL de connexion ou header Authorization)
- Le client WinForms utilise `Microsoft.AspNetCore.SignalR.Client`

### 5.2 Méthodes du hub (appelées par le client)

| Méthode | Paramètres | Description |
|---------|------------|-------------|
| `SendMessage` | `conversationId`, `content` | Envoie un message (persiste puis broadcast) |
| `JoinConversation` | `conversationId` | Rejoint le groupe SignalR de la conversation |
| `LeaveConversation` | `conversationId` | Quitte le groupe SignalR |
| `MarkAsRead` | `conversationId` | Met à jour `LastReadAt` et diffuse `ConversationRead` |
| `GetHistory` | `conversationId`, `page`, `pageSize` | Récupère l'historique paginé |
| `StartTyping` | `conversationId` | Signale que l'utilisateur saisit un message |
| `StopTyping` | `conversationId` | Signale la fin de la saisie |
| `GetOnlineUsers` | — | Retourne les identifiants des utilisateurs ayant au moins une connexion active |

### 5.3 Événements reçus par le client

| Événement | Payload | Description |
|---------|---------|-------------|
| `ReceiveMessage` | `MessageDto` | Nouveau message diffusé aux participants de la conversation |
| `ConversationRead` | `ReadReceiptDto` | Un participant a marqué la conversation comme lue |
| `PresenceChanged` | `PresenceChangedDto` | Un utilisateur passe en ligne ou hors ligne (diffusé à tous) |
| `UserTyping` | `UserTypingDto` | Un autre participant commence ou arrête de saisir |
| `NotificationReceived` | `NotificationDto` | Notification persistée diffusée au groupe `user-{userId}` |
| `MessageUpdated` | `MessageDto` | Un message a été modifié |
| `MessageDeleted` | `MessageDeletedDto` | Un message a été supprimé logiquement |
| `ConversationUpdated` | `ConversationDto` | Membres, titre ou dernier message d'une conversation mis à jour |
| `ConversationDeleted` | `ConversationDeletedDto` | Conversation supprimée ou utilisateur retiré/partant |

Chaque connexion authentifiée est ajoutée au groupe SignalR `user-{userId}` afin de recevoir ses notifications, y compris lorsque l'utilisateur n'a pas rejoint la conversation concernée. Le hub conserve la liste des connexions par utilisateur pour pouvoir retirer immédiatement toutes ses connexions d'un groupe lorsqu'il quitte ou est retiré d'une conversation.

### 5.4 Flux d'envoi de message

1. Client appelle `SendMessage(conversationId, content)` via SignalR
2. Le hub vérifie l'authentification et l'appartenance à la conversation
3. Le message est **persisté en base** (horodatage serveur)
4. Une fois persistant, le message est broadcast via `Clients.Group(conversationId).SendAsync("ReceiveMessage", message)`
5. Les clients connectés reçoivent le message en temps réel

### 5.5 Récupération des messages hors-ligne

Lors de la reconnexion :
1. Le client se reconnecte au hub SignalR
2. Le client appelle `JoinConversation(conversationId)` pour chaque conversation active
3. Le client appelle `GetHistory(conversationId, page, pageSize)` pour récupérer les messages manqués
4. L'API REST `/api/conversations/{id}/messages` peut aussi être utilisée pour l'historique

---

## 6. Services métier

### 6.1 `ConversationService`

- `GetConversationsForUserAsync(userId)` — liste les conversations d'un utilisateur
- `GetConversationByIdAsync(conversationId, userId)` — détail avec vérification d'accès
- `CreateConversationAsync(request, creatorId)` — création privée/groupe avec validation
- `AddParticipantAsync(conversationId, currentUserId, request)` — ajout participant (admin groupe uniquement)
- `RemoveParticipantAsync(conversationId, currentUserId, targetUserId)` — retrait participant, promotion d'un admin restant et nettoyage des notifications
- `LeaveConversationAsync(conversationId, userId)` — quitte un groupe, supprime la conversation si nécessaire
- `DeleteConversationAsync(conversationId, userId)` — suppression complète avec contrôle des droits
- `MarkAsReadAsync(conversationId, userId)` — enregistre `LastReadAt` et retourne un `ReadReceiptDto`
- `GetParticipantUserIdsAsync(conversationId)` — utilisé pour les diffusions et le nettoyage SignalR

### 6.2 `MessageService`

- `SendMessageAsync(conversationId, senderId, request)` — persiste un message (horodatage serveur)
- `SendAttachmentMessageAsync(...)` — persiste un message avec sa pièce jointe
- `GetMessagesAsync(conversationId, userId, page, pageSize)` — historique paginé avec vérification d'accès
- `UpdateMessageAsync(...)` — modification par l'auteur, vérification de participation
- `DeleteMessageAsync(...)` — suppression logique, pièces jointes et notifications associées
- `SearchAsync(userId, query, take)` — recherche contextuelle dans les conversations de l'utilisateur

### 6.3 `AttachmentService`

- `SendAttachmentAsync(...)` — valide le fichier (20 Mo, extensions bloquées, quota 500 Mo), l'écrit sur disque puis crée le message
- `GetDownloadAsync(attachmentId, userId)` — vérifie l'appartenance à la conversation, le message non supprimé, et retourne le fichier
- Nettoyage des fichiers orphelins lors des tâches de maintenance

### 6.4 `NotificationService`

- `CreateMessageNotificationsAsync(...)` — persiste une notification par destinataire et diffuse `NotificationReceived`
- `GetForUserAsync(...)`, `GetUnreadCountAsync(...)` — lecture paginée et compteur
- `MarkAsReadAsync(...)`, `MarkAllAsReadAsync(...)` — lecture d'une ou toutes les notifications
- `MarkConversationAsReadAsync(...)` — marque les notifications d'une conversation comme lues
- `DeleteAsync(...)`, `ClearAsync(...)`, `PurgeReadBeforeAsync(...)` — suppression unitaire, globale et purge
- `DeleteForConversationAsync(...)`, `DeleteForUserConversationAsync(...)` — nettoyage lors des suppressions de conversation/message

### 6.5 `MaintenanceService`

Tâche d'arrière-plan exécutée toutes les 6 heures (hors environnement `Test`) :
- purge des notifications lues anciennes ;
- suppression des pièces jointes orphelines ;
- journalisation des opérations.

### 6.6 `PasswordHasher`

- `Hash(password)` — PBKDF2 hash avec salt aléatoire
- `Verify(password, storedHash)` — vérification à temps constant

### 6.7 `TokenService`

- `GenerateToken(userId, username, role)` — génère un JWT signé

---

## 7. Configuration

### 7.1 `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "JwtSettings": {
    "Key": "",
    "Issuer": "OrgChat",
    "Audience": "OrgChat",
    "ExpirationMinutes": "120"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 7.2 Variables d'environnement recommandées (production)

- `ConnectionStrings__DefaultConnection` — chemin absolu vers `OrgChat.db`
- `JwtSettings__Key` — clé secrète forte (minimum 32 caractères)
- `ASPNETCORE_ENVIRONMENT` — `Production`
- `ASPNETCORE_URLS` — `http://0.0.0.0:5000` (ou IP LAN/VPN spécifique)

### 7.3 Stockage des pièces jointes

Les fichiers sont écrits sous `<ContentRoot>/uploads/{yyyy}/{MM}/`. Ce dossier doit être sauvegardé avec la base de données et ne doit pas être exposé comme répertoire statique. Les téléchargements passent uniquement par `GET /api/attachments/{id}`, protégé par l'authentification et le contrôle de participation.

## 8. Base de données

### 8.1 SQLite (développement / production par défaut)

- Fichier : `OrgChat.db` dans le répertoire de travail du serveur
- Initialisation automatique : `dbContext.Database.Migrate()` au démarrage
- Le fichier ne doit **jamais** être exposé aux clients ni placé sur un partage réseau

### 8.2 Migrations

```powershell
# Créer une nouvelle migration (après modification d'entité)
cd OrgChat.Server
dotnet ef migrations add NomMigration

# Appliquer les migrations
dotnet ef database update
```

### 8.3 Script PostgreSQL

Fichier : `scripts/postgresql/init.sql`  
Nécessite le provider `Npgsql.EntityFrameworkCore.PostgreSQL` (non installé par défaut).

---

## 9. Tests

### 9.1 Types de tests

- **Test de fumée** : `AppDbContextSmokeTests` — vérifie la création du schéma et l'insertion utilisateur
- **Tests d'intégration auth** : `AuthIntegrationTests` — register, login, doublons, mauvais mot de passe
- **Tests d'intégration conversations** : `ConversationIntegrationTests` — création, liste, contrôle d'accès
- **Tests d'intégration messages** : `MessageIntegrationTests` — envoi, pagination, contrôle d'accès
- **Tests d'intégration lecture** : `ReadReceiptIntegrationTests` — compteur de non-lus et accusé de lecture
- **Tests d'intégration pièces jointes** : `AttachmentIntegrationTests` — upload, téléchargement, contrôle d'accès, extensions bloquées
- **Tests d'intégration notifications** : `NotificationIntegrationTests` — persistance, compteur, lecture ciblée et globale
- **Tests de fonctionnalités avancées** : `FeatureIntegrationTests` — édition/suppression de messages, quitter/supprimer une conversation, administration, révocation de jeton, recherche, pagination/suppression des notifications

### 9.2 Exécution

```powershell
cd backend
dotnet test .\OrgChat.sln
```

Le frontend se compile séparément :

```powershell
cd frontend
dotnet build .\OrgChat.Client.sln
```

### 9.3 Base de test

Les tests utilisent une base **InMemory** (via `OrgChatTestFactory`) pour éviter toute dépendance au fichier SQLite physique.

---

## 10. Flux d'utilisation typique

### 10.1 Inscription et connexion

```
Client → POST /api/auth/register  →  JWT token
Client → POST /api/auth/login     →  JWT token
Client → GET  /api/users/me       →  Profil (header: Bearer <token>)
```

### 10.2 Créer une conversation et envoyer un message

```
Client → POST /api/conversations    →  ConversationDto (ID)
Client → POST /hubs/chat (SignalR)
         └── SendMessage(conversationId, "Hello")
         └── Broadcast "ReceiveMessage" aux participants
Client → GET /api/conversations/{id}/messages?page=1&pageSize=50
Client → POST /api/conversations/{id}/read → ReadReceiptDto
         └── Broadcast "ConversationRead" aux participants
```

### 10.3 Reconnexion après hors-ligne

```
Client → SignalR reconnect
Client → JoinConversation(conversationId) (pour chaque conversation)
Client → GetHistory(conversationId, page, pageSize)
```

---

## 11. Limites et scope actuel

### 11.1 Implémenté (cœur de messagerie)

- Authentification JWT, inscription, connexion
- Comptes activés/désactivés
- Conversations privées et groupes
- Envoi et persistance de messages
- Historique paginé
- Diffusion temps réel via SignalR
- Contrôle d'accès par conversation
- Récupération des messages à la reconnexion
- Accusés de lecture et compteurs de non-lus persistants (`ConversationParticipant.LastReadAt`)
- Diffusion temps réel des accusés de lecture (`ConversationRead`)
- Pièces jointes (20 Mo max, quota 500 Mo/conversation, extensions exécutables bloquées, stockage hors web root)
- Présence en ligne/hors ligne et indicateur de saisie via SignalR
- Notifications persistées, compteur non lu, diffusion `NotificationReceived`, historique paginé, suppression et purge
- Édition et suppression logique des messages, avec diffusion temps réel
- Cycle de vie des conversations : quitter un groupe, suppression, promotion automatique d'un admin
- Administration complète : création de compte, rôle, mot de passe, activation/désactivation, suppression/anonymisation
- Recherche de messages contextuelle
- Session client persistante chiffrée DPAPI et mode tray
- Vérification du compte actif à chaque requête et révocation immédiate des jetons désactivés
- Maintenance automatique (notifications anciennes, fichiers orphelins)
- Notifications Windows locales lorsque la fenêtre n'est pas active

### 11.2 Non implémenté (étapes futures)

- Appels audio/vidéo (hors scope)
- Fédération multi-serveurs
- Chiffrement de bout en bout des messages
- Notifications push hors réseau local (le client affiche une notification Windows locale)

---

## 12. Commandes de contrôle

```powershell
# Restaurer les packages
dotnet restore

# Compiler
dotnet build

# Exécuter les tests
dotnet test

# Créer une migration (si modification d'entité)
cd OrgChat.Server
dotnet ef migrations add NomMigration

# Appliquer les migrations
dotnet ef database update

# Lancer le serveur
cd OrgChat.Server
dotnet run
```

---

## 13. Recommandations de déploiement LAN/VPN

1. Lier le serveur sur l'IP LAN/VPN, pas seulement localhost :
   ```json
   // launchSettings.json ou variables d'environnement
   "applicationUrl": "http://0.0.0.0:5000"
   ```

2. Changer la clé JWT par défaut (`JwtSettings:Key`) en production.

3. S'assurer que le fichier `OrgChat.db` est sauvegardé régulièrement.

4. Le client WinForms doit pointer vers `http://<IP-serveur>:5000` pour l'API et `http://<IP-serveur>:5000/hubs/chat` pour SignalR.

5. Aucun port de base de données n'est ouvert sur le réseau (SQLite est fichier local côté serveur).

---

## 14. Fichiers créés (résumé)

### OrgChat.Core
- `Contracts/Auth/RegisterRequest.cs`, `LoginRequest.cs`, `AuthResponse.cs`
- `Contracts/Users/UserDto.cs`, `UpdateUserRequest.cs`
- `Contracts/Conversations/ConversationDto.cs`, `ConversationCreateRequest.cs`, `ConversationParticipantDto.cs`, `AddParticipantRequest.cs`, `ReadReceiptDto.cs`
- `Contracts/Messages/MessageDto.cs`, `MessageCreateRequest.cs`, `MessageUpdateRequest.cs`, `MessageDeletedDto.cs`, `MessageSearchResultDto.cs`, `PagedMessageResponse.cs`, `AttachmentDto.cs`
- `Contracts/Conversations/ConversationDeletedDto.cs`
- `Contracts/Notifications/NotificationDto.cs`
- `Contracts/Realtime/PresenceChangedDto.cs`, `UserTypingDto.cs`
- `Contracts/Users/CreateUserRequest.cs`, `UpdateUserRoleRequest.cs`, `ChangePasswordRequest.cs`, `ResetPasswordRequest.cs`
- `Contracts/Services/IPasswordHasher.cs`, `ITokenService.cs`
- `Entities/*`, `Enums/*`

### OrgChat.Infrastructure
- `Data/AppDbContext.cs`
- `Configurations/*` (6 fichiers de configuration Fluent API)
- `Migrations/20261105000000_InitialCreate.cs`, `AppDbContextModelSnapshot.cs`
- `Services/PasswordHasher.cs`, `TokenService.cs`

### OrgChat.Server
- `Program.cs` (DI, auth JWT, SignalR, migration auto)
- `Controllers/AuthController.cs`, `UsersController.cs`, `ConversationsController.cs`, `MessagesController.cs`, `AttachmentsController.cs`, `NotificationsController.cs`, `SearchController.cs`
- `Hubs/ChatHub.cs`
- `Services/ConversationService.cs`, `MessageService.cs`, `AttachmentService.cs`, `NotificationService.cs`, `MaintenanceService.cs`
- `appsettings.json`, `launchSettings.json`

### OrgChat.Tests
- `OrgChatTestFactory.cs`
- `AuthIntegrationTests.cs`, `ConversationIntegrationTests.cs`, `MessageIntegrationTests.cs`
- `ReadReceiptIntegrationTests.cs`, `AttachmentIntegrationTests.cs`, `NotificationIntegrationTests.cs`
- `FeatureIntegrationTests.cs`
- `AppDbContextSmokeTests.cs`

### Client WinForms (`frontend/OrgChat.Client`)
- `Services/ApiClient.cs`, `RealtimeChatService.cs`, `SessionStore.cs` (DPAPI)
- `Forms/MainForm.cs` (messages, recherche, tray, temps réel)
- `Forms/MessageEditForm.cs`, `MessageSearchForm.cs`, `NotificationsForm.cs`
- `Forms/UsersForm.cs`, `CreateUserForm.cs`, `ChangePasswordForm.cs`, `ProfileForm.cs`
- `UI/MessageBubble.cs` (menu contextuel modifier/supprimer)

### Scripts
- `scripts/postgresql/init.sql`

### Solution
- `OrgChat.sln`

### Documentation
- `DOCUMENTATION_TECHNIQUE.md`, `README.md`

---

## 15. Client Windows Forms — fonctionnalités complémentaires

- **Session persistante** : le jeton et l'URL du serveur sont stockés dans `%LOCALAPPDATA%\OrgChat\session.dat`, chiffrés avec DPAPI (`ProtectedData`, portée utilisateur). La session est restaurée au démarrage via `GET /api/users/me`, puis supprimée si le jeton est invalide ou le compte inactif.
- **Déconnexion** : efface la session DPAPI, ferme réellement la fenêtre principale (`CloseForLogout`) et réaffiche l'écran de connexion.
- **Mode tray** : fermer la fenêtre principale la masque dans la zone de notification ; l'application reste connectée pour recevoir les messages. Le menu du tray permet de rouvrir ou quitter.
- **Recherche** : la barre de recherche principale ouvre `MessageSearchForm` (touche Entrée) et permet d'accéder directement au message dans sa conversation.
- **Messages** : menu contextuel sur ses propres messages pour modifier ou supprimer ; les mises à jour arrivent en temps réel via `MessageUpdated` / `MessageDeleted`.
- **Conversations** : menu contextuel pour quitter un groupe ou supprimer une conversation, avec confirmation.
- **Notifications** : centre de notifications avec pagination, suppression unitaire, « Tout supprimer », marquage lu et navigation vers la conversation.
- **Administration** : `UsersForm` permet de créer un compte, changer un rôle, réinitialiser un mot de passe, activer/désactiver et supprimer/anonymiser un compte.
- **Profil** : modification du profil et changement du mot de passe personnel.
- **Multilinguisme** : le client est disponible en **français** et en **anglais**. Les libellés statiques sont traduits par `Localization.Apply(Control)` (parcours de l'arbre de contrôles, `PlaceholderText`, menus contextuels et `LinkLabel` recalculés) ; les chaînes dynamiques passent par `Localization.T(...)` / `Localization.F(...)`. Le changement de langue reconstruit l'interface sans perdre l'authentification.
- **Thèmes** : `ThemeManager` définit quatre palettes (`Sombre`, `Clair`, `Océan`, `Améthyste`) ; `Palette` agit comme une façade vers le thème courant. Les couleurs `Hero*`, `OnAccent` garantissent la lisibilité sur les thèmes clairs ou à accent clair. Le changement de thème reconstruit également l'interface.
- **Préférences** : langue et thème sont persistés dans `%LOCALAPPDATA%\OrgChat\settings.json` (`SettingsStore`), chargés dans `Program.cs` avant la création des formulaires. Le dialogue `SettingsForm` est accessible depuis l'écran de connexion, l'en-tête de la fenêtre principale et le menu tray.

---

## 16. Plateforme V2 — organisations, RBAC, comptes, supervision, audit

Mission : faire évoluer OrgChat en plateforme professionnelle (multi-organisations,
RBAC 5 rôles, comptes créés par les administrateurs avec activation sécurisée,
départements, supervision, audit), sans réécrire la messagerie existante.

### 16.1 État d'avancement

- ✅ **Socle de données** : `Organization`, `Department`, `DepartmentMember`, `Role`,
  `UserRole`, `Invitation`, `Session`, `AuditLog`, `UserActivity` + extension de `User`
  (prénom/nom, fonction, statut, suspension temporaire, département, `MustChangePassword`).
- ✅ **Migration `PlatformV2` non destructive** : données existantes préservées,
  ancienne colonne `Users.Role` convertie vers `UserRoles` (2 → `OrgAdmin`, sinon `User`),
  triggers d'audit, sauvegarde automatique avant migration.
- ✅ **RBAC** : rôles sémés, `IUserRoleService`, JWT multi-claims `role` + `org`,
  autorisations vérifiées côté serveur, périmètre par organisation.
- ✅ **Comptes** : création par admin (lien d'activation ou mot de passe direct),
  activation, désactivation, suspension temporaire, réactivation, réinitialisation,
  sessions, historique d'activité, import CSV, filtres et pagination.
- ✅ **Organisations et départements** : CRUD, logo, paramètres, responsable, membres, stats.
- ✅ **Supervision** : endpoints de synthèse, utilisateurs, départements, activité,
  santé, connexions, incidents, rapports (métadonnées uniquement).
- ✅ **Audit** : journal append-only paginé/filtrable + facettes, protégé par triggers SQLite,
  isolation par organisation pour les non-SuperAdmin.
- ✅ **Frontend** : tableau de bord par rôle, module de supervision (7 pages, graphiques
  dessinés, tableaux thémés, filtres, pagination), profil/mot de passe, gestion des comptes.
- ✅ **Tests** : 36 tests (authentification, comptes, RBAC, isolation, supervision, audit,
  notifications, messagerie, affectations départementales, migrations non destructives).

### 16.2 Matrice des rôles

| Capacité | SuperAdmin | OrgAdmin | Supervisor | DepartmentManager | User |
|----------|:----------:|:--------:|:----------:|:-----------------:|:----:|
| Créer / gérer les organisations | ✅ | — | — | — | — |
| Désigner `OrgAdmin` / `Supervisor` | ✅ | — | — | — | — |
| Gérer les comptes de son organisation | ✅ | ✅ | — | — | — |
| Gérer les départements | ✅ | ✅ | — | son périmètre | — |
| Superviser (stats, rapports, incidents) | global | son org. | son org. | — | — |
| Lire le journal d'audit | global | son org. | son org. | — | — |
| Messagerie privée / groupes | ✅ | ✅ | ✅ | ✅ | ✅ |

Le `DepartmentManager` dispose de son propre tableau de bord ; les endpoints de
supervision (`/api/supervision/*`, `/api/audit`) sont réservés à
`SuperAdmin`, `OrgAdmin` et `Supervisor`.

### 16.3 Endpoints de supervision (`/api/supervision`)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/summary` | Compteurs : utilisateurs, actifs, suspendus, désactivés, en ligne, départements, conversations, messages 24 h, nouveaux comptes 7 j, sessions actives, connexions simultanées |
| `GET` | `/users?search=&status=&role=&departmentId=&page=&pageSize=` | Comptes paginés avec rôle, département, statut, présence, dernière connexion |
| `GET` | `/departments` | Membres, actifs, en ligne et messages (7 j) par département |
| `GET` | `/activity?days=` | Série quotidienne : connexions, échecs, nouveaux comptes, messages, utilisateurs actifs |
| `GET` | `/connections?hours=` | Sessions et utilisateurs uniques par heure + connexions simultanées courantes |
| `GET` | `/health` | État API / base / SignalR, fournisseur, disponibilité, version, sessions |
| `GET` | `/incidents?days=&page=&pageSize=` | Échecs de connexion, accès refusés et résultats non `Success` |
| `GET` | `/reports?period=daily\|weekly\|monthly` | Synthèse de la période, top utilisateurs, répartition par département |

**Règles de confidentialité** : ces endpoints ne lisent que des compteurs et des
métadonnées (dates, statuts, volumes). Aucun contenu de message ni pièce jointe
n'est retourné. Le périmètre est l'organisation du JWT, sauf SuperAdmin (global).

### 16.4 Endpoints d'audit (`/api/audit`)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/api/audit?search=&action=&entityType=&result=&actorUserId=&from=&to=&page=&pageSize=` | Journal paginé et filtrable |
| `GET` | `/api/audit/facets` | Valeurs distinctes (actions, types d'entité, résultats) pour les filtres |

Le journal est **append-only** : `AuditService` n'expose aucune mise à jour ni suppression,
et les triggers SQLite `AuditLogs_PreventUpdate` / `AuditLogs_PreventDelete` rejettent
toute tentative de falsification. Un non-SuperAdmin ne voit que les entrées dont
l'acteur appartient à son organisation.

### 16.5 Frontend de supervision

- Formulaire `SupervisionForm` avec navigation latérale et 7 pages :
  Vue d'ensemble, Utilisateurs, Activité, Départements, Rapports, Journal d'audit, Santé.
- Graphiques dessinés sans dépendance externe : histogramme (`BarChartPanel`) et
  anneau de répartition (`DonutChartPanel`).
- Tableaux thémés (`ThemedListView`) : en-têtes sombres, lignes alternées, survol,
  sélection douce, cohérents avec les 4 thèmes.
- Filtres (recherche, statut, rôle, action, résultat), pagination, états de chargement,
  messages d'erreur réseau, bouton d'actualisation.
- Traductions FR/EN pour les libellés statiques et dynamiques.

### 16.6 Reste à faire

- 🔜 Tests de non-régression plus étendus sur les parcours UI (tests d'interface).
- 🔜 Notifications temps réel des incidents de sécurité vers les superviseurs.
- 🔜 Export PDF/CSV des rapports de supervision.
- 🔜 Rotation de clé JWT et politique de mot de passe configurable.


---

*Documentation générée pour le backend OrgChat. À maintenir à jour à chaque nouvelle étape d'implémentation.*
