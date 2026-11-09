# OrgChat — Client desktop Windows

Client Windows Forms de messagerie d'entreprise pour l'API OrgChat.

- **Technologie** : C# / .NET 10 / Windows Forms (`net10.0-windows`)
- **Temps réel** : SignalR (`Microsoft.AspNetCore.SignalR.Client`)
- **Transport REST** : `HttpClient` + JSON
- **Projet** : `OrgChat.Client` (assembly `OrgChat.Client`)

L'interface est entièrement construite par code (aucun fichier Designer), avec un thème
configurable (Sombre, Clair, Océan, Améthyste) et des composants personnalisés
(bulles de message, avatars, cartes de conversation).

---

## Prérequis

- Windows 10 ou 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (ou Visual Studio avec la charge de
  travail « Développement desktop .NET »)
- Le serveur OrgChat démarré et accessible

## Lancement du serveur

Depuis la racine du dépôt :

```powershell
dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj
```

Par défaut, le serveur écoute sur `http://localhost:5000` (profil `OrgChat.Server`) :

- API REST : `http://localhost:5000/api/...`
- Hub SignalR : `http://localhost:5000/hubs/chat`

## Lancement du client

Depuis la racine du dépôt :

```powershell
dotnet run --project .\frontend\OrgChat.Client\OrgChat.Client.csproj
```

Ou, depuis `frontend/` :

```powershell
dotnet build .\OrgChat.Client.sln
dotnet run --project .\OrgChat.Client\OrgChat.Client.csproj
```

L'exécutable généré se trouve dans :

```text
frontend\OrgChat.Client\bin\Debug\net10.0-windows\OrgChat.Client.exe
```

---

## Configuration de l'URL du serveur

L'URL de base est résolue dans cet ordre (la dernière valeur gagne) :

1. Valeur par défaut : `http://localhost:5000`
2. Fichier `appsettings.json` (copié dans le dossier de sortie) :

   ```json
   {
     "OrgChat": {
       "BaseUrl": "http://localhost:5000",
       "PageSize": 50
     }
   }
   ```

3. Variable d'environnement `ORGCHAT_BASEURL` :

   ```powershell
   $env:ORGCHAT_BASEURL = "http://192.168.1.10:5000"
   dotnet run --project .\frontend\OrgChat.Client\OrgChat.Client.csproj
   ```

4. Champ **Serveur** de l'écran de connexion (modifiable sans redémarrer le client).

Les URL sans schéma sont acceptées : `192.168.1.10:5000` devient `http://192.168.1.10:5000`.
Les barres obliques finales sont supprimées automatiquement.

`PageSize` contrôle le nombre de messages chargés par page (borné entre 1 et 100).

---

## Langue et thème

Le client est disponible en **français** et en **anglais**, avec quatre thèmes visuels :

- **Clair** (par défaut) — `AppTheme.Light`
- **Sombre** — `AppTheme.Dark`
- **Océan** — `AppTheme.Ocean`
- **Améthyste** — `AppTheme.Amethyst`

Les réglages sont accessibles :

- depuis l'écran de connexion, via le lien **Langue et thème** sous le champ serveur ;
- depuis la fenêtre principale, via le bouton **⚙** de la barre d'en-tête ;
- depuis le menu de l'icône de notification (tray), via **Paramètres**.

Ils sont persistés dans `%LOCALAPPDATA%\OrgChat\settings.json` (JSON lisible, formats
`"French"` / `"English"` et `"Dark"` / `"Light"` / `"Ocean"` / `"Amethyst"`) et rechargés
au démarrage, avant la création des formulaires. Changer la langue ou le thème reconstruit
l'interface sans perdre l'authentification ni la conversation affichée.

---

## Fonctionnalités

### Authentification et session

- Connexion par nom d'utilisateur ou e-mail et mot de passe
- **Aucune auto-inscription** : les comptes sont créés par les administrateurs
- Activation de compte par lien sécurisé (jeton à usage unique) ou mot de passe direct
- Session persistante : jeton et URL serveur stockés dans
  `%LOCALAPPDATA%\OrgChat\session.dat`, chiffrés avec **DPAPI**
- Restauration automatique de la session au démarrage via `GET /api/users/me`
- Session supprimée si le jeton est invalide ou le compte désactivé/suspendu/supprimé
- Déconnexion : efface la session et réaffiche l'écran de connexion

### Tableau de bord et supervision

- Routage par rôle : messagerie pour `User`, tableau de bord pour `SuperAdmin`,
  `OrgAdmin`, `Supervisor` et `DepartmentManager`
- Tableau de bord avec navigation latérale, cartes de statistiques, utilisateurs et départements
- Gestion des départements (`DepartmentsForm`) : création, modification, désactivation,
  responsable, ajout/retrait de membres et statistiques du département
- Interface d'administration à barre latérale bleu nuit, cohérente avec les maquettes
- Module de supervision (`SupervisionForm`) pour `SuperAdmin`, `OrgAdmin`, `Supervisor` :
  - Vue d'ensemble : comptes, actifs, en ligne, sessions, graphiques, incidents
  - Utilisateurs : recherche, statut, rôle, département, présence, dernière connexion
  - Activité : messages et connexions par jour, détail quotidien, connexions simultanées
  - Départements : membres, actifs, en ligne, messages
  - Rapports : quotidien / hebdomadaire / mensuel, top utilisateurs
  - Journal d'audit : filtres action/résultat, pagination, IP
  - Santé : API, base de données, SignalR, version, disponibilité
- Graphiques et tableaux entièrement dessinés selon le thème actif, sans dépendance externe
- **Métadonnées uniquement** : le contenu des conversations privées n'est jamais affiché

### Conversations

- Liste des conversations triées par dernière activité
- Recherche locale par titre ou participant
- Création d'une conversation privée ou d'un groupe
- Aperçu du dernier message et horodatage
- Gestion des participants d'un groupe (ajout / retrait)
- Menu contextuel : quitter un groupe, supprimer une conversation
- Mise à jour temps réel des participants et suppressions

### Messages

- Historique paginé avec **Charger les messages plus anciens**
- Envoi avec `Entrée`, retour à la ligne avec `Maj+Entrée`
- Envoi via SignalR lorsque connecté, repli automatique sur l'API REST sinon
- Réception temps réel des nouveaux messages, modifications et suppressions
- Menu contextuel sur ses propres messages : **Modifier** / **Supprimer**
- Indicateur « modifié » et message supprimé masqué
- Accusés de lecture : `Vu` en privé, `Vu par X/Y` en groupe
- Pièces jointes : upload `multipart/form-data`, légende optionnelle, téléchargement sécurisé
  (limite serveur 20 Mo, extensions exécutables refusées)
- Indicateur de saisie et présence en ligne/hors ligne
- Recherche globale de messages (`Entrée` dans la barre de recherche)

### Notifications et non-lus

- Compteur de non-lus par conversation et total dans le titre de la fenêtre
- Notification Windows lorsqu'un message arrive alors que la fenêtre est inactive
- Clic sur la notification : restauration de la fenêtre et ouverture de la conversation
- Centre de notifications avec pagination, marquage lu, suppression unitaire, « Tout supprimer »
  et navigation vers la conversation
- Purge serveur des notifications lues anciennes

### Utilisateurs, profil et administration

- Annuaire des utilisateurs
- Édition de son profil (nom affiché, e-mail)
- Changement de son propre mot de passe et gestion de ses sessions
- Gestion centralisée des comptes (administrateurs) :
  - création par lien d'activation ou mot de passe direct, identifiant généré
  - modification, activation/désactivation
  - suspension temporaire avec motif et date de fin
  - réinitialisation (jeton, jamais de mot de passe révélé)
  - révocation de sessions, historique de connexions
  - import CSV, recherche et filtres paginés
- Seul le SuperAdmin peut désigner les rôles `OrgAdmin` et `Supervisor`

### Fenêtre et cycle de vie

- Fermer la fenêtre principale la masque dans la **zone de notification (tray)**
- Le menu du tray permet de rouvrir la fenêtre ou quitter complètement
- La déconnexion ferme réellement la fenêtre principale et revient à la connexion

---

## Structure du projet

```text
frontend/
├── OrgChat.Client.sln
├── README.md
└── OrgChat.Client/
    ├── OrgChat.Client.csproj
    ├── appsettings.json
    ├── Program.cs                       # Point d'entrée, gestion des erreurs globales
    ├── App/
    │   └── OrgChatApplicationContext.cs # Navigation connexion ↔ fenêtre principale
    ├── Configuration/
    │   └── ClientOptions.cs             # Chargement appsettings + ORGCHAT_BASEURL
    ├── Models/
    │   └── Dtos.cs                      # DTO alignés sur les contrats du backend
    ├── Services/
    │   ├── ApiClient.cs                 # Tous les appels REST
    │   ├── ApiException.cs              # Erreurs HTTP normalisées
    │   ├── RealtimeChatService.cs       # Hub SignalR, reconnexion, événements
    │   ├── SessionStore.cs              # Session DPAPI
    │   ├── SettingsStore.cs             # Préférences langue/thème (settings.json)
    │   └── Localization.cs              # Traductions FR/EN et application aux contrôles
    ├── Forms/
    │   ├── LoginForm.cs                 # Connexion, activation, URL serveur
    │   ├── MainForm.cs                  # Conversations, messages, recherche, tray
    │   ├── RoleDashboardForm.cs         # Tableau de bord et navigation par rôle
    │   ├── SupervisionForm.cs           # Supervision : 7 pages, filtres, graphiques
    │   ├── SettingsForm.cs              # Langue et thème
    │   ├── NewConversationForm.cs       # Création privée ou groupe
    │   ├── GroupMembersForm.cs          # Participants d'un groupe
    │   ├── MessageEditForm.cs           # Édition d'un message
    │   ├── MessageSearchForm.cs         # Recherche globale
    │   ├── NotificationsForm.cs         # Centre de notifications
    │   ├── ProfileForm.cs               # Édition du profil
    │   ├── ChangePasswordForm.cs        # Mot de passe personnel/admin
    │   ├── CreateUserForm.cs            # Création de compte admin
    │   └── UsersForm.cs                 # Annuaire et administration
    └── UI/
        ├── ThemeManager.cs              # Définitions des thèmes et thème actif
        ├── Palette.cs                   # Façade vers le thème courant
        ├── Ui.cs                        # Fabriques de contrôles et helpers
        ├── RoundedPanel.cs
        ├── RoundedButton.cs
        ├── ThemedListView.cs            # Tableaux détaillés dessinés selon le thème
        ├── Charts.cs                    # Histogramme et anneau de répartition
        ├── AvatarControl.cs
        ├── ConversationItem.cs
        ├── MessageBubble.cs
        ├── UserSelectRow.cs
        └── BrandMark.cs
```

## Notes techniques

- Toutes les URL de l'API sont centralisées dans `Services/ApiClient.cs`.
- Le jeton JWT est fourni à SignalR via `AccessTokenProvider`, sans duplication de logique.
- Les erreurs HTTP sont converties en `ApiException` avec le message renvoyé par le serveur.
- `WFO1000` est désactivé (`NoWarn`) car les contrôles sont créés par code.
- Le client ne référence pas le projet `backend/` ; il communique uniquement par HTTP et WebSocket.

## Limites connues

- Application Windows uniquement (Windows Forms).
- Pas de cache hors ligne : l'historique nécessite une connexion au serveur.
- Les notifications push hors LAN/VPN ne sont pas prises en charge.
