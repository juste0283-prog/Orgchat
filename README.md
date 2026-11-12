# OrgChat

Plateforme de messagerie professionnelle pour organisation, conçue pour un déploiement **LAN/VPN**.

- **Backend** : ASP.NET Core / .NET 10, EF Core, JWT, SignalR
- **Client** : Windows Forms / .NET 10 (`net10.0-windows`)
- **Base (actuelle)** : SQLite (fichier local côté serveur), migrations automatiques au démarrage
- **Préparation migration** : support prévu pour PostgreSQL (provider Npgsql, configuration par `DatabaseProvider`) ; migrations PostgreSQL à générer lors de la bascule
- **Temps réel** : SignalR (`/hubs/chat`)
- **Sécurité** : RBAC 5 rôles, sessions révocables, audit inaltérable, isolation multi-organisations

## Fonctionnalités

### Comptes et sécurité
- **Aucune auto-inscription** : les comptes sont créés exclusivement par les administrateurs.
- Création par **lien d'activation sécurisé** (jeton aléatoire, expiration configurable, usage unique) **ou** avec un mot de passe direct.
- Identifiant généré automatiquement (`prenom.nom`, unicité garantie) et modifiable.
- Mots de passe **PBKDF2-SHA256** : jamais stockés ni consultables en clair.
- Activation / désactivation, **suspension temporaire** avec date de fin et motif, réactivation.
- Réinitialisation par jeton, révocation de sessions, historique de connexions et d'activité.
- Import CSV de comptes, recherche et filtres paginés.
- Session client persistante chiffrée (DPAPI), révocation serveur à chaque requête.

### Rôles et permissions (RBAC)
| Rôle | Portée |
|------|--------|
| `SuperAdmin` | Global : organisations, désignation des `OrgAdmin` et `Supervisor`, supervision et audit complets |
| `OrgAdmin` | Son organisation : comptes (hors rôles désignés par le SuperAdmin), départements, supervision |
| `Supervisor` | Son organisation : lecture de la supervision, des rapports et du journal d'audit |
| `DepartmentManager` | Son département : tableau de bord, membres et statistiques de son périmètre |
| `User` | Messagerie uniquement |

Les permissions sont **vérifiées côté serveur** (`[Authorize(Roles=...)]` + contrôles de périmètre),
pas seulement dans l'interface.

### Multi-organisations et départements
- Isolation stricte des données par organisation (claim `org` du JWT).
- Organisations : nom, logo, coordonnées, paramètres, activation/désactivation.
- Départements : CRUD, responsable, membres, désactivation, statistiques.

### Supervision (métadonnées uniquement)
- Vue d'ensemble : comptes, actifs, en ligne, sessions, conversations, messages 24 h.
- Utilisateurs : statut, rôle, département, présence, dernière connexion.
- Activité : connexions, échecs, messages et comptes créés par jour.
- Départements : membres, actifs, en ligne, volume de messages.
- Rapports **quotidiens / hebdomadaires / mensuels**.
- Santé du service : API, base de données, SignalR, disponibilité, version.
- Connexions simultanées et incidents (échecs de connexion, accès refusés).
- **Le contenu des conversations privées n'est jamais exposé** : seules des métadonnées
  (volumes, dates, compteurs) sont lues.

### Journal d'audit inaltérable
- Acteur, action, entité, identifiant, détails, résultat, IP, horodatage.
- Consultation paginée et filtrable (action, entité, résultat, recherche).
- Écriture seule garantie au niveau applicatif **et** par des triggers SQLite
  (toute `UPDATE`/`DELETE` est rejetée).
- Un non-SuperAdmin ne voit que les entrées dont l'acteur appartient à son organisation.

### Messagerie
- Conversations privées et groupes, participants et administrateurs de groupe.
- Envoi temps réel, historique paginé, édition et suppression logique de ses messages.
- Pièces jointes (20 Mo max, quota 500 Mo par conversation, extensions exécutables bloquées).
- Recherche de messages, accusés de lecture, non-lus, présence et indicateur de saisie.
- Reconnexion automatique et notifications Windows locales.

### Interface client
- Tableau de bord et navigation **propres à chaque rôle**.
- Module de supervision dédié : graphiques dessinés, tableaux thémés, filtres, pagination.
- Tableaux entièrement thémés (en-têtes sombres, lignes alternées, survol, sélection).
- Thèmes **Sombre, Clair, Océan, Améthyste** et interface **français / anglais**.
- États de chargement, messages d'erreur réseau explicites, confirmations.

## Structure

```text
backend/
  OrgChat.Core/            contrats, entités, enums, constantes RBAC/audit
  OrgChat.Infrastructure/  DbContext, configurations, migrations, services
  OrgChat.Server/          API REST, SignalR, supervision, audit, maintenance
  OrgChat.Tests/           tests d'intégration, de migration et de fumée

frontend/
  OrgChat.Client/          client WinForms
  OrgChat.Client.sln

requirement.txt            dépendances SDK/NuGet/outils
DOCUMENTATION_TECHNIQUE.md documentation backend détaillée
```

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 pour le client WinForms
- Aucun serveur de base externe requis en mode actuel : SQLite est utilisé par défaut (fichier `OrgChat.db` côté serveur). PostgreSQL est préparé pour une migration future (serveur PostgreSQL requis lors de la bascule).

## Démarrage rapide

### 1. Backend

```powershell
dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj
```

Le serveur écoute par défaut sur `http://0.0.0.0:5000` (accessible depuis le LAN/VPN si le pare-feu l’autorise) :

- API : `http://localhost:5000/api/...`
- SignalR : `http://localhost:5000/hubs/chat`

La base `OrgChat.db` et les migrations sont initialisées automatiquement, avec
**sauvegarde préalable** dans `backend/OrgChat.Server/backups/`.

### 2. Client

```powershell
dotnet run --project .\frontend\OrgChat.Client\OrgChat.Client.csproj
```

Pour pointer vers un autre serveur, utiliser le champ **Serveur** de l'écran de connexion,
le fichier `frontend/OrgChat.Client/appsettings.json` ou la variable d'environnement
`ORGCHAT_BASEURL`.

### Premier compte (racine)

- Au démarrage, le serveur garantit l'existence d'un compte **SuperAdmin** avec l'identifiant **`superAdmin`** et le mot de passe de bootstrap attendu, afin de permettre l'accès initial à l'administration.
- Ce mot de passe n'est jamais stocké en clair (uniquement son hash PBKDF2 en base) et ne doit pas être consigné dans les fichiers de configuration ou de démonstration ; communiquez-le et gérez-le selon la politique de sécurité de votre organisation.
- Le SuperAdmin peut ensuite gérer les organisations et créer les comptes `OrgAdmin` / `Supervisor`.

## Tests

```powershell
cd backend
dotnet test .\OrgChat.sln
```

Les tests utilisent une base **InMemory** (sauf le test de migration, qui utilise SQLite).
Ils couvrent notamment : authentification, activation, comptes, sessions, RBAC,
isolation inter-organisations, départements, supervision, audit, notifications,
messagerie, migrations non destructives et non-régression.

## Configuration

`backend/OrgChat.Server/appsettings.json` (exemple sans secrets, SQLite actif par défaut) :

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=OrgChat.db;Foreign Keys=True",
    "PostgreSQL": "Host=localhost;Port=5432;Database=orgchat;Username=postgres;Password=<secret>"
  },
  "JwtSettings": {
    "Key": "",
    "Issuer": "OrgChat",
    "Audience": "OrgChat",
    "ExpirationMinutes": "120"
  },
  "OrgChat": {
    "PublicBaseUrl": ""
  }
}
```

- **Aucun secret ne doit être commité**. Fournissez-les via des variables d'environnement ou une configuration sécurisée (ex. `ConnectionStrings__DefaultConnection`, `ConnectionStrings__PostgreSQL`, `JwtSettings__Key`).
- `DatabaseProvider` : `Sqlite` (actuel, par défaut) ou `PostgreSQL`/`Npgsql` (préparé pour une migration future).
- `OrgChat:PublicBaseUrl` : URL publique du serveur (ex. `https://chat.example.org`) utilisée dans les liens d'activation. Vide = déduite de la requête HTTP.
- `SmtpSettings:Host` vide : aucun e-mail n'est envoyé ; le lien d'activation est copiable depuis l'interface.

## Base de données, migrations et préparation PostgreSQL

### Actuel : SQLite (actif)
- **SGBD** : SQLite (fichier `OrgChat.db` côté serveur), utilisé via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`). Aucun mot de passe de base de données n’est requis.
- **Migrations** : migration initiale SQLite fournie (`Migrations/*InitialSqlite*`). Au démarrage, le serveur applique automatiquement les migrations (`MigrateAsync()`), avec sauvegarde préalable de la base dans `backups/` quand le fichier existe (`backend/scripts/backup-database.ps1`).
- **Sécurité/secret JWT** : `JwtSettings:Key` doit être défini en production (variable d’environnement `JwtSettings__Key` ou configuration sécurisée). En `Development`, une clé de secours locale est utilisée si la clé n’est pas fournie, pour faciliter l’exécution.
- **Échange multi-utilisateurs (LAN/VPN)** : les clients ne se connectent pas directement à la base ; ils passent tous par le serveur (API + SignalR) qui lit/écrit dans la base et diffuse les messages en temps réel. Pour un usage multi-utilisateurs sur le même sous-réseau/VPN :
  - lancer le serveur sur une machine accessible (`http://0.0.0.0:5000` par défaut en dev), ouvrir le port 5000 si nécessaire ;
  - configurer chaque client avec `http://<IP-ou-hôte-serveur>:5000` ;
  - créer/activer les comptes utilisateurs (SuperAdmin garanti : `superAdmin` / `superadmin123!`).
  - la base SQLite doit rester locale au serveur (ne pas la partager comme fichier réseau).

### Préparation : PostgreSQL (future migration)
- Le projet est **préparé** pour PostgreSQL : le package `Npgsql.EntityFrameworkCore.PostgreSQL` est référencé et le serveur peut sélectionner le provider via `DatabaseProvider` (`Sqlite` par défaut, `PostgreSQL`/`Npgsql` prévu) avec la chaîne `ConnectionStrings:PostgreSQL`.
- **Migrations** : les migrations EF Core sont spécifiques au provider ; les migrations actuelles ciblent SQLite. Lors d’une migration réelle vers PostgreSQL, il faudra générer/appliquer un jeu de migrations PostgreSQL (ex. `dotnet ef migrations add InitialPostgres -p backend/OrgChat.Infrastructure -s backend/OrgChat.Server`) sur une base PostgreSQL, puis migrer les données depuis `OrgChat.db` si nécessaire (ETL/outils comme `pgloader`).
- **Sauvegarde PostgreSQL** : script préparé `backend/scripts/backup-database-postgresql.ps1` (via `pg_dump`). Pour de fortes charges/concurrences ou un déploiement distribué, PostgreSQL est recommandé.

## Documentation

- [DOCUMENTATION_TECHNIQUE.md](DOCUMENTATION_TECHNIQUE.md) — architecture, modèle de données,
  migrations, API REST, RBAC, supervision, audit, configuration, tests et déploiement.
- [frontend/README.md](frontend/README.md) — guide du client Windows.
- [requirement.txt](requirement.txt) — dépendances SDK/NuGet et outils.
