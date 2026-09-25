using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using HeatingSystems.App.Services;
using HeatingSystems.App.ViewModels;
using HeatingSystems.App.Views;
using HeatingSystems.Core.Localization;
using HeatingSystems.Data;

namespace HeatingSystems.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, DatabaseInitializer.CatalogFileName);
            var databasePath = DatabaseInitializer.EnsureUserDatabase(bundled, DatabaseInitializer.DefaultUserDatabasePath);
            var factory = new SqliteConnectionFactory(databasePath);
            var settings = new SqliteSettingsRepository(factory);

            // Language: saved choice, English by default.
            var language = Enum.TryParse<AppLanguage>(settings.Get(MainViewModel.LanguageSettingKey), out var saved)
                ? saved
                : AppLanguage.English;
            Localizer.SetLanguage(language);
            ApplyCulture();
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag)));
            // Subscribed before the view models, so formatting culture is switched before pages refresh.
            Localizer.LanguageChanged += (_, _) =>
            {
                ApplyCulture();
                if (MainWindow is not null) MainWindow.Language = XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
            };

            var viewModel = new MainViewModel(new SqliteCatalogRepository(factory), new SqliteProjectRepository(factory), settings,
                new DialogService());
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Localizer.F("error.startup", ex.Message), Localizer.T("app.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ApplyCulture()
    {
        var culture = Localizer.Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(Localizer.F("error.unexpected", e.Exception.Message), Localizer.T("app.title"), MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
