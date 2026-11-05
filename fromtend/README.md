# OrgChat — Client desktop

Client Windows de messagerie d'entreprise pour l'API OrgChat.

- **Technologie** : C# / .NET 10 / Windows Forms
- **Temps réel** : SignalR (`Microsoft.AspNetCore.SignalR.Client`)
- **Transport REST** : `HttpClient` + JSON
- **Projet** : `OrgChat.Client` (assembly `OrgChat.Client`, cible `net10.0-windows`)

L'interface est entièrement construite par code (aucun fichier Designer), avec un thème sombre
et des composants personnalisés (bulles de message, avatars, cartes de conversation).

---

## Prérequis

- Windows 10 ou 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (ou une version de Visual Studio prenant en charge .NET 10, avec la charge de travail « Développement desktop .NET »)
- Le serveur OrgChat démarré et accessible

## Lancement du serveur

Le backend n'est pas modifié par ce projet. Depuis la racine du dépôt :

```powershell
dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj
```

Par défaut, le serveur écoute sur `http://localhost:5000` (profil `OrgChat.Server`) :

- API REST : `http://localhost:5000/api/...`
- Hub SignalR : `http://localhost:5000/hubs/chat`

## Lancement du client

```powershell
dotnet run --project .\fromtend\OrgChat.Client\OrgChat.Client.csproj
```

Ou, depuis `fromtend/` :

```powershell
dotnet build .\OrgChat.Client.sln
dotnet run --project .\OrgChat.Client\OrgChat.Client.csproj
```

L'exécutable généré se trouve dans :

```
fromtend\OrgChat.Client\bin\Debug\net10.0-windows\OrgChat.Client.exe
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
   dotnet run --project .\fromtend\OrgChat.Client\OrgChat.Client.csproj
   ```

4. Champ **Serveur** de l'écran de connexion (modifiable sans redémarrer le client).

Les URL sans schéma sont acceptées : `192.168.1.10:5000` devient `http://192.168.1.10:5000`.
Les barres obliques finales sont supprimées automatiquement.

`PageSize` contrôle le nombre de messages chargés par page (borné entre 1 et 100).

---

## Fonctionnalités

### Authentification

- Connexion par nom d'utilisateur ou e-mail et mot de passe
- Création de compte (nom d'affichage, nom d'utilisateur, e-mail, mot de passe)
- Jeton JWT conservé en mémoire pour la session (aucune persistance sur disque)
- Déconnexion et retour à l'écran de connexion

### Conversations

- Liste des conversations de l'utilisateur, triées par dernière activité
- Recherche locale par titre ou par participant
- Création d'une conversation privée (exactement un autre participant)
- Création d'un groupe (titre + au moins un autre participant)
- Aperçu du dernier message et de son horodatage
- Gestion des participants d'un groupe (ajout / retrait)

### Messages

- Historique paginé avec le bouton **Charger les messages plus anciens**
- Envoi avec `Entrée`, retour à la ligne avec `Maj+Entrée`
- Envoi via SignalR lorsque la connexion est active, repli automatique sur l'API REST sinon
- Réception en temps réel des messages des autres membres
- Affichage chronologique, bulles alignées selon l'auteur, heure d'envoi
- Reconnexion automatique du hub SignalR

### Utilisateurs et profil

- Annuaire des utilisateurs
- Édition de son profil (nom affiché, e-mail, mot de passe)
- Administration : désactivation / réactivation de comptes (si le compte connecté est administrateur)

---

## Structure du projet

```
fromtend/
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
    │   └── RealtimeChatService.cs       # Hub SignalR, reconnexion, événements
    ├── Forms/
    │   ├── LoginForm.cs                 # Connexion / inscription / URL serveur
    │   ├── MainForm.cs                  # Conversations, messages, temps réel
    │   ├── NewConversationForm.cs       # Création privée ou groupe
    │   ├── GroupMembersForm.cs          # Participants d'un groupe
    │   ├── ProfileForm.cs               # Édition du profil
    │   └── UsersForm.cs                 # Annuaire et administration
    └── UI/
        ├── Palette.cs                   # Couleurs du thème
        ├── Ui.cs                        # Fabriques de contrôles et helpers de mise en page
        ├── RoundedPanel.cs
        ├── RoundedButton.cs
        ├── AvatarControl.cs
        ├── ConversationItem.cs
        ├── MessageBubble.cs
        ├── UserSelectRow.cs
        └── BrandMark.cs
```

## Notes techniques

- Toutes les URL de l'API sont centralisées dans `Services/ApiClient.cs`.
- Le jeton JWT est fourni à SignalR via `AccessTokenProvider`, sans duplication de logique d'authentification.
- Les erreurs HTTP sont converties en `ApiException` avec le message renvoyé par le serveur.
- `WFO1000` est désactivé (`NoWarn`) car les contrôles sont créés par code, sans sérialisation Designer.
- Le client ne modifie ni ne référence le projet `backend/` ; il communique uniquement par HTTP et WebSocket.

## Limites connues

- Application Windows uniquement (Windows Forms).
- Pas de pièces jointes, d'indicateur de saisie, d'accusés de lecture ni de notifications système.
- Pas d'édition / suppression de message.
- Pas de cache hors ligne : l'historique nécessite une connexion au serveur.
- Le jeton n'est pas persisté : il faut se reconnecter à chaque lancement.
- Le thème est sombre et n'est pas configurable.
