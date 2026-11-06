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
| `User` | `Users` | Utilisateur : username, email, display name, hash de mot de passe, rôle, statut actif |
| `Conversation` | `Conversations` | Conversation : titre, type (privé/groupe), créateur, date |
| `ConversationParticipant` | `ConversationParticipants` | Lien utilisateur ↔ conversation : date de jointure, dernière lecture, admin de groupe |
| `Message` | `Messages` | Message : conversation, expéditeur, contenu, horodatage serveur, état édité/supprimé |
| `Attachment` | `Attachments` | Pièce jointe : message, nom original, nom de stockage, type, taille, chemin |
| `Notification` | `Notifications` | Notification : utilisateur, type, payload JSON, date de lecture |

### 2.2 Contraintes clés

- `Users.Username` — unique, indexé
- `Users.Email` — unique, indexé
- `ConversationParticipants(ConversationId, UserId)` — unique composite
- `Messages.ConversationId` — indexé
- `Messages(ConversationId, CreatedAt)` — index composé pour l'historique paginé
- Suppression en cascade : conversation → participants, messages, attachments ; message → attachments ; utilisateur → notifications
- Suppression restreinte : conversation → créateur ; message → expéditeur ; participant → utilisateur

### 2.3 Script PostgreSQL

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
| Compte désactivé bloqué | Vérification `IsActive` à la connexion |
| Messages d'erreur génériques | "Identifiants invalides" (ne révèle pas si l'utilisateur existe) |
| Autorisation API | `[Authorize]` sur tous les endpoints sensibles ; `[Authorize(Roles = "Admin")]` pour l'administration |
| Autorisation SignalR | `[Authorize]` sur le `ChatHub` ; vérification d'appartenance à la conversation |
| Horodatage serveur | `CreatedAt` défini par le serveur ; le client ne fournit jamais de date de message |
| Persistance avant diffusion | Message sauvegardé en base avant broadcast SignalR |

## 4. API REST — Référence des endpoints

### 4.1 Authentification

| Méthode | Endpoint | Corps | Réponse | Description |
|---------|----------|-------|---------|-------------|
| `POST` | `/api/auth/register` | `RegisterRequest` | `AuthResponse` | Inscription d'un nouvel utilisateur |
| `POST` | `/api/auth/login` | `LoginRequest` | `AuthResponse` | Connexion, retour du jeton JWT |

**RegisterRequest :**
```json
{
  "username": "string",
  "email": "string",
  "displayName": "string",
  "password": "string"
}
```

**AuthResponse :**
```json
{
  "token": "string (JWT)",
  "userId": 0,
  "username": "string",
  "email": "string",
  "displayName": "string",
  "role": "User | Admin"
}
```

### 4.2 Utilisateurs

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/users/me` | Authentifié | Profil de l'utilisateur connecté |
| `GET` | `/api/users` | Authentifié | Liste de tous les utilisateurs (infos publiques) |
| `GET` | `/api/users/{id}` | Authentifié | Profil d'un utilisateur par ID |
| `PUT` | `/api/users/{id}` | Authentifié / Admin | Modifie un profil (soi-même ou admin) |
| `PUT` | `/api/users/{id}/deactivate` | Admin | Désactive un compte |
| `PUT` | `/api/users/{id}/reactivate` | Admin | Réactive un compte |

**UserDto :**
```json
{
  "id": 0,
  "username": "string",
  "email": "string",
  "displayName": "string",
  "role": "User | Admin",
  "isActive": true
}
```

### 4.3 Conversations

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/conversations` | Authentifié | Liste des conversations de l'utilisateur |
| `GET` | `/api/conversations/{id}` | Authentifié + participant | Détail d'une conversation |
| `POST` | `/api/conversations` | Authentifié | Crée une conversation privée ou groupe |
| `POST` | `/api/conversations/{id}/read` | Authentifié + participant | Marque la conversation comme lue et diffuse l'accusé `ConversationRead` |
| `POST` | `/api/conversations/{id}/participants` | Authentifié / admin groupe | Ajoute un participant |
| `DELETE` | `/api/conversations/{id}/participants/{userId}` | Authentifié / admin groupe | Retire un participant |

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
- Un groupe ou privé ne peut descendre en dessous de 2 participants

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

**Pièces jointes :**
- Champ `file` obligatoire, champ `content` optionnel (légende).
- Taille maximale : 20 Mo ; extensions exécutables bloquées (`.exe`, `.bat`, `.ps1`, `.dll`, etc.).
- Stockage hors web root dans `uploads/{année}/{mois}/{guid}.{ext}` sous le répertoire de contenu du serveur.
- Le téléchargement vérifie que l'utilisateur est participant de la conversation du message.
- Le message et sa pièce jointe sont diffusés via `ReceiveMessage` comme un message classique.

### 4.5 Notifications

| Méthode | Endpoint | Autorisation | Description |
|---------|----------|--------------|-------------|
| `GET` | `/api/notifications?take=100` | Authentifié | Liste des notifications de l'utilisateur (max 200) |
| `GET` | `/api/notifications/unread-count` | Authentifié | Nombre de notifications non lues |
| `POST` | `/api/notifications/{id}/read` | Authentifié | Marque une notification comme lue |
| `POST` | `/api/notifications/read-all` | Authentifié | Marque toutes les notifications comme lues |

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

Chaque connexion authentifiée est ajoutée au groupe SignalR `user-{userId}` afin de recevoir ses notifications, y compris lorsque l'utilisateur n'a pas rejoint la conversation concernée.

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
- `RemoveParticipantAsync(conversationId, currentUserId, targetUserId)` — retrait participant
- `MarkAsReadAsync(conversationId, userId)` — enregistre `LastReadAt` et retourne un `ReadReceiptDto`

### 6.2 `MessageService`

- `SendMessageAsync(conversationId, senderId, request)` — persiste un message (horodatage serveur)
- `SendAttachmentMessageAsync(...)` — persiste un message avec sa pièce jointe
- `GetMessagesAsync(conversationId, userId, page, pageSize)` — historique paginé avec vérification d'accès

### 6.3 `AttachmentService`

- `SendAttachmentAsync(...)` — valide le fichier (20 Mo, extensions bloquées), l'écrit sur disque puis crée le message
- `GetDownloadAsync(attachmentId, userId)` — vérifie l'appartenance à la conversation et retourne le fichier

### 6.4 `NotificationService`

- `CreateMessageNotificationsAsync(...)` — persiste une notification par destinataire et diffuse `NotificationReceived`
- `GetForUserAsync(...)`, `GetUnreadCountAsync(...)` — lecture et compteur
- `MarkAsReadAsync(...)`, `MarkAllAsReadAsync(...)` — lecture d'une ou toutes les notifications
- `MarkConversationAsReadAsync(...)` — marque les notifications d'une conversation comme lues

### 6.5 `PasswordHasher`

- `Hash(password)` — PBKDF2 hash avec salt aléatoire
- `Verify(password, storedHash)` — vérification à temps constant

### 6.6 `TokenService`

- `GenerateToken(userId, username, role)` — génère un JWT signé

---

## 7. Configuration

### 7.1 `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=OrgChat.db"
  },
  "JwtSettings": {
    "Key": "votre-cle-secrete-de-developpement-a-changer-en-production-min-32-caracteres",
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

### 9.2 Exécution

```powershell
cd c:\Users\AWALOU SADOU\Desktop\app-reseau
dotnet test
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
- Pièces jointes (20 Mo max, extensions exécutables bloquées, stockage hors web root)
- Présence en ligne/hors ligne et indicateur de saisie via SignalR
- Notifications persistées, compteur non lu, diffusion `NotificationReceived` et historique consultable

### 11.2 Non implémenté (étapes futures)

- Notifications système push (le client affiche une notification Windows locale)
- Administration complète
- Appels audio (hors scope)
- Édition / suppression de message

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
- `Contracts/Messages/MessageDto.cs`, `MessageCreateRequest.cs`, `PagedMessageResponse.cs`, `AttachmentDto.cs`
- `Contracts/Notifications/NotificationDto.cs`
- `Contracts/Realtime/PresenceChangedDto.cs`, `UserTypingDto.cs`
- `Contracts/Services/IPasswordHasher.cs`, `ITokenService.cs`
- `Entities/*`, `Enums/*`

### OrgChat.Infrastructure
- `Data/AppDbContext.cs`
- `Configurations/*` (6 fichiers de configuration Fluent API)
- `Migrations/20261105000000_InitialCreate.cs`, `AppDbContextModelSnapshot.cs`
- `Services/PasswordHasher.cs`, `TokenService.cs`

### OrgChat.Server
- `Program.cs` (DI, auth JWT, SignalR, migration auto)
- `Controllers/AuthController.cs`, `UsersController.cs`, `ConversationsController.cs`, `MessagesController.cs`, `AttachmentsController.cs`, `NotificationsController.cs`
- `Hubs/ChatHub.cs`
- `Services/ConversationService.cs`, `MessageService.cs`, `AttachmentService.cs`, `NotificationService.cs`
- `appsettings.json`, `launchSettings.json`

### OrgChat.Tests
- `OrgChatTestFactory.cs`
- `AuthIntegrationTests.cs`, `ConversationIntegrationTests.cs`, `MessageIntegrationTests.cs`
- `ReadReceiptIntegrationTests.cs`, `AttachmentIntegrationTests.cs`, `NotificationIntegrationTests.cs`
- `AppDbContextSmokeTests.cs`

### Scripts
- `scripts/postgresql/init.sql`

### Solution
- `OrgChat.sln`

### Documentation
- `DOCUMENTATION_TECHNIQUE.md`

---

*Documentation générée pour le backend OrgChat. À maintenir à jour à chaque nouvelle étape d'implémentation.*
