# Base de données SQLite

Le backend OrgChat utilise EF Core Code-First et SQLite. Le fichier de base de
données reste sur le serveur; les clients ne l'ouvrent pas directement.

## MCD

```text
Utilisateur (créateur) 1 ───── 0..N Conversation
Utilisateur 1 ───── 0..N Participant N..0 ───── 1 Conversation
Utilisateur (expéditeur) 1 ───── 0..N Message N..0 ───── 1 Conversation
```

Une conversation possède plusieurs participants et messages. La relation
plusieurs-à-plusieurs entre utilisateurs et conversations est portée par
`ConversationParticipants`, qui contient aussi les données propres à la
participation. Les règles métier du backend exigent au moins deux participants
pour une conversation.

## MLD et dictionnaire de données initial

`PK` désigne la clé primaire, `FK` une clé étrangère. Les dates sont enregistrées
en UTC par le backend.

| Table | Colonne | Type logique SQLite | Null | Règle |
|---|---|---|---|---|
| `Users` | `Id` | INTEGER | non | PK autoincrémentée |
| | `Username` | TEXT(100) | non | unique, index |
| | `Email` | TEXT(256) | non | unique, index |
| | `DisplayName` | TEXT(100) | non | |
| | `PasswordHash` | TEXT | non | hash uniquement, jamais le mot de passe |
| | `Role` | INTEGER | non | défaut `1`; valeurs `1` (User), `2` (Admin) |
| | `IsActive` | INTEGER/BOOLEAN | non | défaut `true` |
| | `CreatedAt` | TEXT/DateTime | non | héritée de `BaseEntity` |
| `Conversations` | `Id` | INTEGER | non | PK autoincrémentée |
| | `Title` | TEXT(200) | oui | |
| | `Type` | INTEGER | non | `1` (Private), `2` (Group) |
| | `CreatedById` | INTEGER | non | FK → `Users.Id`, suppression restreinte |
| | `CreatedAt` | TEXT/DateTime | non | héritée de `BaseEntity` |
| `ConversationParticipants` | `Id` | INTEGER | non | PK autoincrémentée |
| | `ConversationId` | INTEGER | non | FK → `Conversations.Id`, cascade |
| | `UserId` | INTEGER | non | FK → `Users.Id`, suppression restreinte |
| | `JoinedAt` | TEXT/DateTime | non | valeur UTC par défaut |
| | `LastReadAt` | TEXT/DateTime | oui | |
| | `IsGroupAdmin` | INTEGER/BOOLEAN | non | défaut `false` |
| | `CreatedAt` | TEXT/DateTime | non | héritée de `BaseEntity` |
| `Messages` | `Id` | INTEGER | non | PK autoincrémentée |
| | `ConversationId` | INTEGER | non | FK → `Conversations.Id`, cascade |
| | `SenderId` | INTEGER | non | FK → `Users.Id`, suppression restreinte |
| | `Content` | TEXT | non | |
| | `CreatedAt` | TEXT/DateTime | non | horodatage UTC serveur |
| | `EditedAt` | TEXT/DateTime | oui | |
| | `IsDeleted` | INTEGER/BOOLEAN | non | défaut `false` |
| | `DeletedAt` | TEXT/DateTime | oui | |

### Relations et index

- `Users.Username` et `Users.Email` sont uniques.
- `ConversationParticipants(ConversationId, UserId)` est unique; `UserId`
  possède aussi un index.
- `Messages(ConversationId, CreatedAt)` indexe l'historique; `SenderId` est
  également indexé.
- La suppression d'une conversation supprime ses participants et messages.
  Les utilisateurs référencés comme créateur, participant ou expéditeur sont
  protégés par des suppressions restreintes.
- Les configurations EF Core correspondantes sont dans
  `OrgChat.Infrastructure/Configurations`.

## Migration et mise à jour du schéma

La migration `20261105000000_InitialCreate` crée déjà les tables `Users`,
`Conversations`, `ConversationParticipants` et `Messages`, avec leurs clés,
contraintes et index. Elle crée également les tables complémentaires présentes
dans le modèle EF Core. Il ne faut donc pas créer une seconde migration pour
ces tables.

Depuis `backend`, après installation du SDK .NET 10 et de l'outil `dotnet-ef` :

```powershell
dotnet ef database update --project OrgChat.Infrastructure --startup-project OrgChat.Server
```

L'API applique également les migrations au démarrage hors environnement de test.

## Jeux de données de test et vérifications

Sur une base de test déjà migrée, `SqliteDatabaseTestData.SeedDemoDataAsync`
crée des données déterministes et réexécutables : deux utilisateurs de
démonstration, une conversation de groupe, ses participants et un message.
Leurs valeurs `PasswordHash` sont volontairement invalides : ces comptes ne
doivent pas être utilisés pour se connecter.

`SqliteDatabaseVerification.VerifyAsync` vérifie l'intégrité SQLite, les clés
étrangères, les index essentiels et les doublons de noms d'utilisateur et
d'adresses e-mail. Le résultat expose `IsValid` et le détail de chaque contrôle.

Exemple après application de la migration :

```csharp
await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

await SqliteDatabaseTestData.SeedDemoDataAsync(db);

var verification = await SqliteDatabaseVerification.VerifyAsync(db);
if (!verification.IsValid)
{
    throw new InvalidOperationException(
        $"Vérification SQLite échouée: {verification}");
}
```

L'API expose l'historique paginé à
`GET /api/conversations/{id}/messages?page=1&pageSize=50`. La taille est limitée
à 100 éléments et le tri déterministe utilise `CreatedAt`, puis `Id`, en ordre
décroissant.
Le backend réalise cette pagination avec `OrderBy`/`ThenBy`, `Skip` et `Take`
dans `MessageService`.

## Sauvegarde et restauration

`OrgChat.Infrastructure.Data.SqliteDatabaseMaintenance` fournit les méthodes
`Backup(databasePath, backupPath)` et `RestoreBackup(backupPath, databasePath)`.
Elles utilisent l'API de sauvegarde SQLite en ligne, puis vérifient l'intégrité,
les clés étrangères et la présence du schéma OrgChat. Elles lèvent une
exception en cas de fichier invalide ou d'échec; elles ne remplacent pas une
sauvegarde hors site.

Exemple d'appel dans un outil d'administration .NET :

```csharp
SqliteDatabaseMaintenance.Backup("OrgChat.db", "backups/OrgChat.backup.db");
SqliteDatabaseMaintenance.RestoreBackup("backups/OrgChat.backup.db", "OrgChat.db");
```

La restauration doit être faite serveur arrêté, sans autre connexion ouverte
sur le fichier cible. Conserver les sauvegardes dans un emplacement protégé et
tester périodiquement une restauration sur une copie de la base.
