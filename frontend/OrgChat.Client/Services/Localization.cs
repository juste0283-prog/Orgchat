using System.Globalization;

namespace OrgChat.Client.Services;

/// <summary>Langues disponibles dans le client.</summary>
public enum AppLanguage
{
    French,
    English
}

/// <summary>
/// Traductions français → anglais et application automatique aux contrôles.
/// Les formulaires restent écrits en français : le texte source sert de clé.
/// </summary>
public static class Localization
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        // ---------------------------------------------------------- Commun
        ["Enregistrer"] = "Save",
        ["Annuler"] = "Cancel",
        ["Fermer"] = "Close",
        ["Créer"] = "Create",
        ["Création…"] = "Creating…",
        ["Ajouter"] = "Add",
        ["Retirer"] = "Remove",
        ["Supprimer"] = "Delete",
        ["Modifier"] = "Edit",
        ["Rechercher"] = "Search",
        ["Recherche…"] = "Searching…",
        ["Chargement…"] = "Loading…",
        ["Aucun résultat."] = "No results.",
        ["Aucun message pour le moment"] = "No messages yet",
        ["Conversation privée"] = "Private conversation",
        ["Nouveau message"] = "New message",
        ["Quelqu’un"] = "Someone",
        ["Opération annulée."] = "Operation cancelled.",
        ["Hier"] = "Yesterday",
        ["o"] = "B",
        ["Ko"] = "KB",
        ["Mo"] = "MB",
        ["Utilisateur"] = "User",
        ["Administrateur"] = "Administrator",
        ["admin"] = "admin",
        ["utilisateur"] = "user",
        ["actif"] = "active",
        ["inactif"] = "inactive",
        ["vous"] = "you",
        [" (vous)"] = " (you)",
        ["Group"] = "Group",
        ["Private"] = "Private",
        ["User"] = "User",
        ["Admin"] = "Admin",

        // ------------------------------------------------------- Paramètres
        ["Paramètres"] = "Settings",
        ["Langue et thème"] = "Language and theme",
        ["Langue"] = "Language",
        ["Thème"] = "Theme",
        ["Français"] = "French",
        ["English"] = "English",
        ["Sombre"] = "Dark",
        ["Clair"] = "Light",
        ["Océan"] = "Ocean",
        ["Améthyste"] = "Amethyst",
        ["Les changements s'appliquent immédiatement."] = "Changes are applied immediately.",

        // ----------------------------------------------------- Authentification
        ["OrgChat · Connexion"] = "OrgChat · Sign in",
        ["Bienvenue"] = "Welcome",
        ["Rejoignez votre organisation en quelques secondes."] = "Join your organisation in seconds.",
        ["Connectez-vous à votre espace OrgChat."] = "Sign in to your OrgChat workspace.",
        ["Créer un compte"] = "Create account",
        ["Créer mon compte"] = "Create my account",
        ["Se connecter"] = "Sign in",
        ["Déjà un compte ? Se connecter"] = "Already have an account? Sign in",
        ["Pas encore de compte ? Créer un compte"] = "No account yet? Create one",
        ["Première connexion"] = "First sign-in",
        ["Définissez votre mot de passe avec le lien d'activation fourni par votre administrateur."] = "Set your password using the activation link from your administrator.",
        ["Jeton d'activation"] = "Activation token",
        ["Collez le lien ou le jeton reçu"] = "Paste the link or token received",
        ["Nouveau mot de passe"] = "New password",
        ["Activer et se connecter"] = "Activate and sign in",
        ["Première connexion ? Activer mon compte"] = "First sign-in? Activate my account",
        ["Déjà activé ? Se connecter"] = "Already activated? Sign in",
        ["Le jeton d'activation est requis."] = "Activation token is required.",
        ["Le nouveau mot de passe doit contenir au moins 8 caractères."] = "The new password must be at least 8 characters long.",
        ["Activation…"] = "Activating…",
        ["Dashboard Super administrateur"] = "Super administrator dashboard",
        ["Dashboard administrateur"] = "Administrator dashboard",
        ["Tableau de bord superviseur"] = "Supervisor dashboard",
        ["Dashboard responsable de département"] = "Department manager dashboard",
        ["Ouvrir la messagerie"] = "Open messaging",
        ["Gérer les utilisateurs"] = "Manage users",
        ["Voir les départements"] = "View departments",
        ["Supervision (métadonnées)"] = "Supervision (metadata)",
        ["Mon profil"] = "My profile",
        ["Nom d'utilisateur"] = "Username",
        ["Identifiant de connexion"] = "Login identifier",
        ["Votre identifiant"] = "Your username",
        ["Nom affiché"] = "Display name",
        ["Ex. Awa Diallo"] = "e.g. Awa Diallo",
        ["Adresse e-mail"] = "Email address",
        ["vous@organisation.local"] = "you@organisation.local",
        ["Mot de passe"] = "Password",
        ["Votre mot de passe"] = "Your password",
        ["6 caractères minimum"] = "At least 6 characters",
        ["8 caractères minimum"] = "At least 8 characters",
        ["Confirmer le mot de passe"] = "Confirm password",
        ["Répétez le mot de passe"] = "Repeat the password",
        ["Serveur OrgChat"] = "OrgChat server",
        ["La messagerie interne de votre organisation, sur le réseau local ou via VPN."] = "Your organisation's internal messaging, on the local network or over VPN.",
        ["Messages instantanés entre collègues"] = "Instant messages between colleagues",
        ["Conversations privées et groupes"] = "Private conversations and groups",
        ["Historique conservé côté serveur"] = "History stored on the server",
        ["Indiquez l'adresse du serveur OrgChat."] = "Enter the OrgChat server address.",
        ["Le nom d'utilisateur est requis."] = "Username is required.",
        ["Le mot de passe est requis."] = "Password is required.",
        ["Saisissez une adresse e-mail valide."] = "Enter a valid email address.",
        ["Indiquez une adresse e-mail valide."] = "Enter a valid email address.",
        ["Le mot de passe doit contenir au moins 6 caractères."] = "The password must contain at least 6 characters.",
        ["Les deux mots de passe ne correspondent pas."] = "The two passwords do not match.",
        ["Création du compte…"] = "Creating account…",
        ["Connexion…"] = "Signing in…",

        // ------------------------------------------------------ Mot de passe
        ["Changer mon mot de passe"] = "Change my password",
        ["Réinitialiser le mot de passe"] = "Reset password",
        ["Mot de passe actuel"] = "Current password",
        ["Votre mot de passe actuel"] = "Your current password",
        ["Nouveau mot de passe"] = "New password",
        ["Au moins 8 caractères"] = "At least 8 characters",
        ["Confirmer le nouveau mot de passe"] = "Confirm new password",
        ["Indiquez votre mot de passe actuel."] = "Enter your current password.",
        ["Le nouveau mot de passe doit contenir au moins {0} caractères."] = "The new password must contain at least {0} characters.",
        ["Définissez un nouveau mot de passe pour @{0}."] = "Set a new password for @{0}.",
        ["Choisissez un mot de passe d'au moins 8 caractères."] = "Choose a password of at least 8 characters.",
        ["Le mot de passe de @{0} a été réinitialisé."] = "The password for @{0} has been reset.",
        ["Mot de passe réinitialisé"] = "Password reset",
        ["Mot de passe modifié"] = "Password changed",
        ["Votre mot de passe a été modifié."] = "Your password has been changed.",

        // ---------------------------------------------------------- Création
        ["Le compte pourra se connecter immédiatement."] = "The account can sign in immediately.",
        ["Rôle"] = "Role",
        ["Créer le compte"] = "Create account",
        ["Le mot de passe doit contenir au moins {0} caractères."] = "The password must contain at least {0} characters.",
        ["Responsable de département"] = "Department manager",
        ["Superviseur"] = "Supervisor",
        ["Administrateur"] = "Administrator",
        ["Compte actif"] = "Active account",
        ["Lien d'activation (copiable)"] = "Activation link (copyable)",
        ["Copier"] = "Copy",
        ["Copié"] = "Copied",
        ["Prénom"] = "First name",
        ["Fonction"] = "Job title",
        ["Ajouter un utilisateur"] = "Add user",
        ["Mot de passe (optionnel)"] = "Password (optional)",
        ["Laisser vide : lien d'activation"] = "Leave empty: activation link",
        ["Compte créé : @{0}. Le compte est directement prêt à l'emploi."] = "Account created: @{0}. The account is ready to use immediately.",
        ["Compte créé : @{0}. Lien à transmettre à l'utilisateur."] = "Account created: @{0}. Link to pass to the user.",
        ["Un lien d'activation sera généré. Cet utilisateur définira son mot de passe à la première connexion."] = "An activation link will be generated. This user will define their password at first sign-in.",

        // ------------------------------------------------------- Profil
        ["Mon profil"] = "My profile",
        ["Le nom d'utilisateur et le rôle ne peuvent pas être modifiés depuis cet écran."] = "The username and role cannot be changed from this screen.",
        ["Votre nom"] = "Your name",
        ["Le nom affiché est requis."] = "Display name is required.",
        ["Enregistrement…"] = "Saving…",

        // ------------------------------------------------------- Conversations
        ["+  Nouvelle conversation"] = "+  New conversation",
        ["Nouvelle conversation"] = "New conversation",
        ["Profil"] = "Profile",
        ["Annuaire"] = "Directory",
        ["Déconnexion"] = "Sign out",
        ["Charger les messages plus anciens"] = "Load older messages",
        ["Bienvenue sur OrgChat"] = "Welcome to OrgChat",
        ["Sélectionnez une conversation pour commencer."] = "Select a conversation to get started.",
        ["Membres"] = "Members",
        ["Créez une conversation privée ou un groupe pour démarrer."] = "Create a private conversation or a group to get started.",
        ["Astuce : Entrée envoie un message, Maj+Entrée ajoute une ligne."] = "Tip: Enter sends a message, Shift+Enter adds a new line.",
        ["Écrivez un message…"] = "Write a message…",
        ["Ouvrir OrgChat"] = "Open OrgChat",
        ["Quitter"] = "Quit",
        ["Connexion impossible"] = "Unable to connect",
        ["Vérifiez que le serveur OrgChat est démarré et joignable, puis utilisez le bouton ⟳."] = "Check that the OrgChat server is running and reachable, then use the ⟳ button.",
        [" · admin"] = " · admin",
        ["Aucune conversation"] = "No conversations",
        ["Aucune conversation."] = "No conversations.",
        ["Quitter le groupe"] = "Leave group",
        ["Supprimer la conversation"] = "Delete conversation",
        ["Quitter « {0} » ? Vous ne recevrez plus ses messages."] = "Leave \"{0}\"? You will no longer receive its messages.",
        ["Supprimer définitivement « {0} » pour tous les participants ?"] = "Permanently delete \"{0}\" for all participants?",
        ["Conversation supprimée"] = "Conversation deleted",
        ["Sélectionnez une autre conversation."] = "Select another conversation.",
        ["{0} est en train d’écrire…"] = "{0} is typing…",
        ["{0} et {1} écrivent…"] = "{0} and {1} are typing…",
        ["{0} personnes écrivent…"] = "{0} people are typing…",
        ["Écrivez un message…  (Entrée pour envoyer)"] = "Write a message…  (Enter to send)",
        ["Sélectionnez une conversation pour écrire."] = "Select a conversation to write.",
        ["Joindre un fichier"] = "Attach a file",
        ["Tous les fichiers (*.*)|*.*"] = "All files (*.*)|*.*",
        ["Le fichier sélectionné est vide."] = "The selected file is empty.",
        ["La pièce jointe dépasse la taille maximale de 20 Mo."] = "The attachment exceeds the 20 MB maximum size.",
        ["Pièce jointe"] = "Attachment",
        ["Supprimer ce message ? Cette action est définitive."] = "Delete this message? This action is permanent.",
        ["Supprimer le message"] = "Delete message",
        ["Vu par {0}/{1}"] = "Seen by {0}/{1}",
        ["Vu"] = "Seen",
        ["OrgChat ({0})"] = "OrgChat ({0})",
        ["OrgChat — {0} message(s) non lu(s)"] = "OrgChat — {0} unread message(s)",
        ["{0} : {1}"] = "{0}: {1}",
        ["● En ligne"] = "● Online",
        ["● Connexion…"] = "● Connecting…",
        ["● Reconnexion…"] = "● Reconnecting…",
        ["● Hors ligne"] = "● Offline",
        ["Voulez-vous vous déconnecter d'OrgChat ?"] = "Do you want to sign out of OrgChat?",
        ["OrgChat continue en arrière-plan"] = "OrgChat keeps running in the background",
        ["Double-cliquez sur l'icône pour rouvrir la fenêtre."] = "Double-click the icon to reopen the window.",
        ["Groupe #{0}"] = "Group #{0}",
        ["Conversation #{0}"] = "Conversation #{0}",
        ["{0} participants"] = "{0} participants",
        ["{0} participants · {1}"] = "{0} participants · {1}",
        ["Copier le message"] = "Copy message",
        [" · modifié"] = " · edited",

        // ------------------------------------------------------- Recherche
        ["Rechercher dans les messages"] = "Search messages",
        ["Retrouvez un message dans toutes vos conversations. Double-cliquez sur un résultat pour l'ouvrir."] = "Find a message across all your conversations. Double-click a result to open it.",
        ["Rechercher un mot ou une phrase…"] = "Search for a word or phrase…",
        ["Saisissez au moins 2 caractères."] = "Enter at least 2 characters.",
        ["Aucun message trouvé."] = "No messages found.",
        ["{0} résultat(s) — double-cliquez pour ouvrir."] = "{0} result(s) — double-click to open.",

        // -------------------------------------------------- Nouvelle conversation
        ["Choisissez un collègue ou créez un groupe de discussion."] = "Choose a colleague or create a discussion group.",
        ["Discussion privée"] = "Private conversation",
        ["Groupe"] = "Group",
        ["Nom du groupe"] = "Group name",
        ["Ex. Équipe Réseau"] = "e.g. Network Team",
        ["Rechercher un collègue…"] = "Search colleagues…",
        ["Sélectionnez un collègue"] = "Select a colleague",
        ["Sélectionnez un collègue pour démarrer une discussion privée."] = "Select a colleague to start a private conversation.",
        ["Sélectionnez au moins un collègue. Le groupe inclura votre compte."] = "Select at least one colleague. The group will include your account.",
        ["Aucun collègue trouvé."] = "No colleagues found.",
        ["Sélectionnez le collègue avec qui discuter."] = "Select the colleague to chat with.",
        ["Donnez un nom au groupe."] = "Give the group a name.",
        ["Sélectionnez au moins un collègue pour le groupe."] = "Select at least one colleague for the group.",

        // ------------------------------------------------------- Participants
        ["Membres du groupe"] = "Group members",
        ["Participants"] = "Participants",
        ["Ajouter un participant"] = "Add a participant",
        ["Admin du groupe"] = "Group admin",
        ["Aucun participant."] = "No participants.",
        ["{0} membres · {1}"] = "{0} members · {1}",
        ["Conversation privée entre deux participants"] = "Private conversation between two participants",
        ["Retirer {0} de cette conversation ?"] = "Remove {0} from this conversation?",
        ["Confirmer"] = "Confirm",

        // ------------------------------------------------------- Notifications
        ["Notifications"] = "Notifications",
        ["Tout supprimer"] = "Delete all",
        ["Tout marquer comme lu"] = "Mark all as read",
        ["Charger plus"] = "Load more",
        ["Impossible de charger les notifications."] = "Unable to load notifications.",
        ["Aucune notification pour le moment."] = "No notifications yet.",
        ["{0} notification(s) affichée(s) · {1} non lue(s)"] = "{0} notification(s) shown · {1} unread",
        ["{0} notification(s) affichée(s) · tout est lu"] = "{0} notification(s) shown · all read",
        ["Supprimer toutes vos notifications ? Cette action est définitive."] = "Delete all your notifications? This action is permanent.",

        // ------------------------------------------------------------ Annuaire
        ["Annuaire de l'organisation"] = "Organisation directory",
        ["Rechercher un compte…"] = "Search accounts…",
        ["Aucun compte trouvé."] = "No accounts found.",
        ["{0} compte(s) · {1} actif(s)"] = "{0} account(s) · {1} active",
        ["Gérer"] = "Manage",
        ["Rétrograder en utilisateur"] = "Demote to user",
        ["Promouvoir administrateur"] = "Promote to administrator",
        ["Désactiver le compte"] = "Deactivate account",
        ["Réactiver le compte"] = "Reactivate account",
        ["Supprimer le compte"] = "Delete account",
        ["retirer le rôle administrateur à"] = "remove the administrator role from",
        ["promouvoir administrateur"] = "promote to administrator",
        ["Voulez-vous {0} {1} (@{2}) ?"] = "Do you want to {0} {1} (@{2})?",
        ["Changer le rôle"] = "Change role",
        ["Supprimer le compte de {0} (@{1}) ?\n\nLe compte sera anonymisé et ne pourra plus se connecter. Les messages existants sont conservés."] = "Delete the account of {0} (@{1})?\n\nThe account will be anonymized and can no longer sign in. Existing messages are kept.",
        ["désactiver"] = "deactivate",
        ["réactiver"] = "reactivate",
        ["Voulez-vous {0} le compte de {1} (@{2}) ?"] = "Do you want to {0} the account of {1} (@{2})?",

        // --------------------------------------------------------- Messages
        ["Modifier le message"] = "Edit message",
        ["Le message portera la mention « modifié »."] = "The message will be marked as edited.",
        ["Le message ne peut pas être vide."] = "The message cannot be empty.",

        // --------------------------------------------------------- Supervision
        ["Vue d'ensemble"] = "Overview",
        ["Comptes, statuts, départements et dernières connexions."] = "Accounts, statuses, departments and latest sign-ins.",
        ["Connexions et volumes de messages — métadonnées uniquement."] = "Sign-ins and message volumes — metadata only.",
        ["Répartition des membres et de l'activité par département."] = "Member and activity distribution by department.",
        ["Synthèse quotidienne, hebdomadaire et mensuelle."] = "Daily, weekly and monthly summary.",
        ["Traçabilité inaltérable des actions administratives et de sécurité."] = "Tamper-proof trace of administrative and security actions.",
        ["Disponibilité de l'API, de la base de données et de SignalR."] = "API, database and SignalR availability.",
        ["Vue d'ensemble de l'activité et de la santé de la plateforme."] = "Overview of platform activity and health.",
        ["Chargement des données de supervision…"] = "Loading supervision data…",
        ["▦  Vue d'ensemble"] = "▦  Overview",
        ["◉  Utilisateurs"] = "◉  Users",
        ["↗  Activité"] = "↗  Activity",
        ["▭  Départements"] = "▭  Departments",
        ["▤  Rapports"] = "▤  Reports",
        ["⛨  Journal d'audit"] = "⛨  Audit log",
        ["♥  Santé du service"] = "♥  Service health",
        ["←  Retour au tableau de bord"] = "←  Back to dashboard",
        ["Actualiser"] = "Refresh",
        ["Filtrer"] = "Filter",
        ["← Précédent"] = "← Previous",
        ["Suivant →"] = "Next →",
        ["Comptes de l'organisation"] = "Organisation accounts",
        ["Comptes utilisables"] = "Usable accounts",
        ["Présence temps réel"] = "Real-time presence",
        ["Jetons valides"] = "Valid tokens",
        ["Messages envoyés · 14 derniers jours"] = "Messages sent · last 14 days",
        ["Répartition des comptes"] = "Account breakdown",
        ["Incidents récents"] = "Recent incidents",
        ["Comptes"] = "Accounts",
        ["Nom affiché"] = "Display name",
        ["Dernière connexion"] = "Last sign-in",
        ["Tous les statuts"] = "All statuses",
        ["Actifs"] = "Active",
        ["Suspendus"] = "Suspended",
        ["Désactivés"] = "Disabled",
        ["Tous les rôles"] = "All roles",
        ["Responsable de département"] = "Department manager",
        ["Superviseur"] = "Supervisor",
        ["Administrateur d'organisation"] = "Organisation administrator",
        ["Super administrateur"] = "Super administrator",
        ["Page {0} / {1} · {2} utilisateur(s)"] = "Page {0} / {1} · {2} user(s)",
        ["Page {0} / {1} · {2} entrée(s)"] = "Page {0} / {1} · {2} entry(ies)",
        ["7 derniers jours"] = "Last 7 days",
        ["14 derniers jours"] = "Last 14 days",
        ["30 derniers jours"] = "Last 30 days",
        ["90 derniers jours"] = "Last 90 days",
        ["Messages par jour"] = "Messages per day",
        ["Connexions par jour"] = "Sign-ins per day",
        ["Détail quotidien"] = "Daily detail",
        ["Connexions"] = "Sign-ins",
        ["Échecs"] = "Failures",
        ["Utilisateurs actifs"] = "Active users",
        ["Nouveaux comptes"] = "New accounts",
        ["Connexions simultanées : {0} · utilisateurs en ligne : {1}"] = "Concurrent connections: {0} · online users: {1}",
        ["Départements de l'organisation"] = "Organisation departments",
        ["Messages (7 j)"] = "Messages (7 d)",
        ["Rapport quotidien"] = "Daily report",
        ["Rapport hebdomadaire"] = "Weekly report",
        ["Rapport mensuel"] = "Monthly report",
        ["Du {0} au {1} · {2} compte(s) suspendu(s)"] = "From {0} to {1} · {2} suspended account(s)",
        ["Utilisateurs les plus actifs"] = "Most active users",
        ["Journal d'audit (append-only)"] = "Audit log (append-only)",
        ["Rechercher une action, un acteur…"] = "Search an action, an actor…",
        ["Toutes les actions"] = "All actions",
        ["Tous les résultats"] = "All results",
        ["Entité"] = "Entity",
        ["Date"] = "Date",
        ["Acteur"] = "Actor",
        ["Action"] = "Action",
        ["Résultat"] = "Result",
        ["Département"] = "Department",
        ["Départements"] = "Departments",
        ["Statut"] = "Status",
        ["En ligne"] = "Online",
        ["Utilisateurs"] = "Users",
        ["Activité"] = "Activity",
        ["Incidents"] = "Incidents",
        ["Détails"] = "Details",
        ["API REST"] = "REST API",
        ["Base de données"] = "Database",
        ["Sessions actives"] = "Active sessions",
        ["Détails du service"] = "Service details",
        ["État global"] = "Overall status",
        ["Version"] = "Version",
        ["Fournisseur de base"] = "Database provider",
        ["Disponibilité"] = "Uptime",
        ["Heure serveur (UTC)"] = "Server time (UTC)",
        ["Utilisateurs en ligne"] = "Online users",
        ["Connexions SignalR simultanées"] = "Concurrent SignalR connections",
        ["Suspendu"] = "Suspended",
        ["Désactivé"] = "Disabled",
        ["Actif"] = "Active",
        ["Healthy"] = "Healthy",
        ["Degraded"] = "Degraded",
        ["Unavailable"] = "Unavailable",
        ["Système"] = "System",
        ["Aucune donnée sur la période."] = "No data for this period.",
        ["Aucune donnée."] = "No data.",

        // ------------------------------------------------------- Erreurs API
        ["Identifiants invalides."] = "Invalid credentials.",
        ["Compte désactivé ou supprimé."] = "Account disabled or deleted.",
        ["Ce nom d'utilisateur est déjà utilisé."] = "This username is already taken.",
        ["Cette adresse e-mail est déjà utilisée."] = "This email address is already in use.",
        ["Le mot de passe est incorrect."] = "The password is incorrect.",
        ["Conversation introuvable."] = "Conversation not found.",
        ["Message introuvable."] = "Message not found.",
        ["Accès refusé."] = "Access denied."
    };

    private static readonly Dictionary<string, string> ReverseEnglish =
        English
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.French;

    public static bool IsEnglish => CurrentLanguage == AppLanguage.English;

    public static void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
    }

    /// <summary>Traduit un texte source français.</summary>
    public static string T(string french)
    {
        if (string.IsNullOrEmpty(french))
            return french;

        if (IsEnglish)
            return English.TryGetValue(french, out var translated) ? translated : french;

        return ReverseEnglish.TryGetValue(french, out var source) ? source : french;
    }

    /// <summary>Traduit puis formate un texte source français.</summary>
    public static string F(string french, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(french), args);

    /// <summary>Traduit les libellés statiques de l'arbre de contrôles.</summary>
    public static void Apply(Control root)
    {
        if (!IsEnglish)
            return;

        ApplyControl(root);
    }

    private static void ApplyControl(Control control)
    {
        if (control is not TextBoxBase and not ComboBox and not ListBox)
        {
            var translated = T(control.Text);
            if (!string.Equals(translated, control.Text, StringComparison.Ordinal))
            {
                control.Text = translated;
                if (control is LinkLabel link)
                    link.LinkArea = new LinkArea(0, link.Text.Length);
            }
        }

        if (control is TextBox textBox)
            textBox.PlaceholderText = T(textBox.PlaceholderText);

        if (control.ContextMenuStrip is { } menu)
            ApplyMenu(menu);

        foreach (Control child in control.Controls)
            ApplyControl(child);
    }

    private static void ApplyMenu(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
            ApplyMenuItem(item);
    }

    private static void ApplyMenuItem(ToolStripItem item)
    {
        item.Text = T(item.Text ?? string.Empty);
        if (item is ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0)
        {
            foreach (ToolStripItem child in menuItem.DropDownItems)
                ApplyMenuItem(child);
        }
    }
}
