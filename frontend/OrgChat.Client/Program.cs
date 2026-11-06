using OrgChat.Client.App;
using OrgChat.Client.Configuration;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) => ShowFatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                ShowFatalError(exception);
        };

        var settings = SettingsStore.Load();
        Localization.SetLanguage(settings.Language);
        ThemeManager.SetTheme(settings.Theme);

        var options = ClientOptions.Load();
        using var api = new ApiClient(options.BaseUrl);

        using var context = new OrgChatApplicationContext(api, options);
        Application.Run(context);
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            MessageBox.Show(
                "Une erreur inattendue est survenue :\n\n" + exception.Message,
                "OrgChat",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Ignorer : l'application est déjà en cours d'arrêt.
        }
    }
}
