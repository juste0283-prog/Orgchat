# OrgChat

Application de messagerie instantanée pour organisation, conçue pour un déploiement **LAN/VPN**.

- **Backend** : ASP.NET Core / .NET 10, EF Core, SQLite, JWT, SignalR
- **Client** : Windows Forms / .NET 10 (`net10.0-windows`)
- **Base** : SQLite par défaut, migration automatique au démarrage
- **Temps réel** : SignalR (`/hubs/chat`)

## Fonctionnalités

### Messagerie
- Conversations privées et groupes
- Envoi de messages en temps réel, historique paginé
- Édition et suppression logique de ses propres messages
- Pièces jointes (20 Mo max, quota 500 Mo par conversation, extensions exécutables bloquées)
- Recherche de messages dans toutes ses conversations
- Accusés de lecture, non-lus et présence en ligne
- Indicateur de saisie

### Conversations
- Ajout/retrait de participants avec contrôle des droits
- Quitter un groupe, suppression d'une conversation
- Promotion automatique d'un administrateur restant
- Diffusion temps réel des mises à jour et suppressions
- Nettoyage des notifications, pièces jointes et groupes SignalR

### Administration
- Création de compte
- Changement de rôle
- Changement/réinitialisation de mot de passe (8 caractères minimum)
- Activation/désactivation
- Suppression avec anonymisation
- Protection du dernier administrateur actif
- Vérification du compte actif à chaque requête : un JWT d'un compte désactivé/supprimé est rejeté

### Client Windows
- Session persistante chiffrée avec DPAPI (`%LOCALAPPDATA%\OrgChat\session.dat`)
- Mode tray : fermer la fenêtre la masque sans couper la connexion
- Notifications Windows locales
- Centre de notifications avec pagination, suppression et purge
- Menus contextuels messages/conversations
- Écrans d'administration et de profil

## Structure

```text
backend/
  OrgChat.Core/            contrats, entités, enums
  OrgChat.Infrastructure/  DbContext, configurations, migrations, services
  OrgChat.Server/          API REST, SignalR, maintenance
  OrgChat.Tests/           tests d'intégration et de fumée

frontend/
  OrgChat.Client/          client WinForms
  OrgChat.Client.sln
```

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 pour le client WinForms
- Aucun serveur de base externe : SQLite est utilisé par défaut

## Démarrage rapide

### 1. Backend

```powershell
dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj
```

Le serveur écoute par défaut sur `http://localhost:5000` :

- API : `http://localhost:5000/api/...`
- SignalR : `http://localhost:5000/hubs/chat`

La base `OrgChat.db` et les migrations sont initialisées automatiquement.

### 2. Client

```powershell
dotnet run --project .\frontend\OrgChat.Client\OrgChat.Client.csproj
```

Pour pointer vers un autre serveur, utiliser le champ **Serveur** de l'écran de connexion,
le fichier `frontend/OrgChat.Client/appsettings.json` ou la variable d'environnement
`ORGCHAT_BASEURL`.

### Comptes de démonstration

Si la base de démonstration `backend/OrgChat.Server/OrgChat.db` est présente :

| Identifiant | Mot de passe |
|-------------|--------------|
| `demoalice` | `Demo123!` |
| `demobob` | `Demo123!` |

## Tests

```powershell
cd backend
dotnet test .\OrgChat.sln
```

Les tests utilisent une base **InMemory**. Ils couvrent l'authentification, les conversations,
les messages, les accusés de lecture, les pièces jointes, les notifications, l'administration,
la recherche et la révocation des comptes désactivés.

## Configuration

`backend/OrgChat.Server/appsettings.json` :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=OrgChat.db"
  },
  "JwtSettings": {
    "Key": "clé-secrète-minimum-32-caractères",
    "Issuer": "OrgChat",
    "Audience": "OrgChat",
    "ExpirationMinutes": "120"
  }
}
```

En production, remplacer impérativement `JwtSettings:Key` et utiliser des variables
d'environnement pour les secrets.

## Documentation

- [DOCUMENTATION_TECHNIQUE.md](DOCUMENTATION_TECHNIQUE.md) — architecture, API REST,
  événements SignalR, sécurité, services, tests et déploiement.
- [frontend/README.md](frontend/README.md) — guide du client Windows.
