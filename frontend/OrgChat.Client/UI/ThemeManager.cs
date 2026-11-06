namespace OrgChat.Client.UI;

/// <summary>Thèmes visuels proposés par le client.</summary>
public enum AppTheme
{
    Dark,
    Light,
    Ocean,
    Amethyst
}

/// <summary>Définition complète d'une palette de couleurs.</summary>
public sealed record ThemeDefinition(
    AppTheme Id,
    string DisplayName,
    Color Background,
    Color Sidebar,
    Color Surface,
    Color SurfaceAlt,
    Color SurfaceHover,
    Color Border,
    Color Accent,
    Color AccentHover,
    Color AccentSoft,
    Color OnAccent,
    Color Text,
    Color Muted,
    Color Faint,
    Color Success,
    Color Warning,
    Color Danger,
    Color BubbleIn,
    Color HeroStart,
    Color HeroEnd,
    Color HeroText,
    Color HeroMuted);

/// <summary>Source unique du thème actif. La palette est reconstruite à chaque changement.</summary>
public static class ThemeManager
{
    private static readonly ThemeDefinition[] Themes =
    {
        new(
            AppTheme.Dark,
            "Sombre",
            Hex("#0E1116"), Hex("#12161D"), Hex("#171C24"), Hex("#1E242E"),
            Hex("#232A36"), Hex("#2A3341"), Hex("#4F8DFD"), Hex("#6A9FFD"),
            Hex("#243B63"), Color.White, Hex("#E9EEF6"), Hex("#8C98AA"),
            Hex("#637085"), Hex("#3DD68C"), Hex("#F2B84B"), Hex("#EF5B68"),
            Hex("#1C2330"), Hex("#101B2E"), Hex("#0B0F15"), Hex("#E9EEF6"), Hex("#8C98AA")),
        new(
            AppTheme.Light,
            "Clair",
            Hex("#F1F5F9"), Hex("#FFFFFF"), Hex("#FFFFFF"), Hex("#E2E8F0"),
            Hex("#E8EEF5"), Hex("#CBD5E1"), Hex("#2563EB"), Hex("#1D4ED8"),
            Hex("#DBEAFE"), Color.White, Hex("#0F172A"), Hex("#475569"),
            Hex("#94A3B8"), Hex("#15803D"), Hex("#B45309"), Hex("#DC2626"),
            Hex("#E2E8F0"), Hex("#E2E8F0"), Hex("#F8FAFC"), Hex("#0F172A"), Hex("#475569")),
        new(
            AppTheme.Ocean,
            "Océan",
            Hex("#071A1F"), Hex("#0A2229"), Hex("#0F2A32"), Hex("#153943"),
            Hex("#1B4652"), Hex("#24525E"), Hex("#2DD4BF"), Hex("#5EEAD4"),
            Hex("#134E4A"), Hex("#042F2E"), Hex("#E6FFFA"), Hex("#8FB8B3"),
            Hex("#5F8E8A"), Hex("#34D399"), Hex("#FBBF24"), Hex("#F87171"),
            Hex("#12343D"), Hex("#062A33"), Hex("#041216"), Hex("#ECFEFF"), Hex("#8FB8B3")),
        new(
            AppTheme.Amethyst,
            "Améthyste",
            Hex("#120E1D"), Hex("#181226"), Hex("#1F1830"), Hex("#2A2140"),
            Hex("#34284D"), Hex("#44365F"), Hex("#A78BFA"), Hex("#C4B5FD"),
            Hex("#3B2A63"), Hex("#1B1033"), Hex("#F5F3FF"), Hex("#B4A7CC"),
            Hex("#8273A0"), Hex("#4ADE80"), Hex("#FBBF24"), Hex("#FB7185"),
            Hex("#2A2140"), Hex("#1A1130"), Hex("#0E0918"), Hex("#F5F3FF"), Hex("#B4A7CC"))
    };

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static ThemeDefinition Current =>
        Themes.First(theme => theme.Id == CurrentTheme);

    public static IReadOnlyList<ThemeDefinition> All => Themes;

    public static void SetTheme(AppTheme theme)
    {
        CurrentTheme = Themes.Any(candidate => candidate.Id == theme)
            ? theme
            : AppTheme.Dark;
    }

    public static ThemeDefinition Get(AppTheme theme) =>
        Themes.FirstOrDefault(candidate => candidate.Id == theme) ?? Themes[0];

    private static Color Hex(string value) => ColorTranslator.FromHtml(value);
}
