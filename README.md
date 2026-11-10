# OrgChat

Plateforme de messagerie professionnelle pour organisation, conçue pour un déploiement **LAN/VPN**.

- **Backend** : ASP.NET Core / .NET 10, EF Core, SQLite, JWT, SignalR
- **Client** : Windows Forms / .NET 10 (`net10.0-windows`)
- **Base** : SQLite par défaut, migrations automatiques **non destructives** au démarrage
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
- Aucun serveur de base externe : SQLite est utilisé par défaut

## Démarrage rapide

### 1. Backend

```powershell
dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj
```

Le serveur écoute par défaut sur `http://localhost:5000` :

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

| Identifiant | Mot de passe | Environnement |
|-------------|--------------|---------------|
| `superAdmin` | `superadmin123!` | **Développement uniquement** |

- En `Development`, le compte est créé une seule fois s'il n'existe pas (jamais réinitialisé).
- En `Production`, aucun compte par défaut n'est créé : définir explicitement
  `OrgChat:SuperAdminPassword` pour amorcer le compte racine, puis **changer le mot de passe**.
- Le SuperAdmin peut ensuite modifier ses propres identifiants et créer les comptes
  `OrgAdmin` / `Supervisor` de chaque organisation.

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
  },
  "OrgChat": {
    "EnableSelfRegistration": false,
    "InvitationExpirationHours": 72,
    "PublicBaseUrl": ""
  },
  "SmtpSettings": {
    "Host": ""
  }
}
```

- `SmtpSettings:Host` vide : aucun e-mail n'est envoyé ; le lien d'activation est copiable depuis l'interface.
- `OrgChat:PublicBaseUrl` : URL publique du serveur (ex. `https://chat.example.org`) utilisée dans les liens d'activation. Vide = déduite de la requête HTTP.
- `OrgChat:SuperAdminPassword` : amorçage du compte racine en Production.
- En production, remplacer impérativement `JwtSettings:Key` et utiliser des variables
  d'environnement pour tous les secrets.

## Sauvegarde et migrations

- Les migrations sont **additives** : aucune table métier n'est supprimée, les données existantes
  sont conservées et converties (migration `PlatformV2`).
- Une sauvegarde de la base est créée automatiquement avant migration.
- Sauvegarde manuelle recommandée avant toute mise à jour :
  arrêter le serveur puis copier `backend/OrgChat.Server/OrgChat.db`.
- Restauration : remplacer le fichier de base par la sauvegarde, serveur arrêté.

## Documentation

- [DOCUMENTATION_TECHNIQUE.md](DOCUMENTATION_TECHNIQUE.md) — architecture, modèle de données,
  migrations, API REST, RBAC, supervision, audit, configuration, tests et déploiement.
- [frontend/README.md](frontend/README.md) — guide du client Windows.
- [requirement.txt](requirement.txt) — dépendances SDK/NuGet et outils.
