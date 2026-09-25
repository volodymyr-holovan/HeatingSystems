using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using HeatingSystems.App.Services;
using HeatingSystems.App.ViewModels;
using HeatingSystems.App.Views;
using HeatingSystems.Data;

namespace HeatingSystems.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = CultureInfo.GetCultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, DatabaseInitializer.CatalogFileName);
            var databasePath = DatabaseInitializer.EnsureUserDatabase(bundled, DatabaseInitializer.DefaultUserDatabasePath);
            var factory = new SqliteConnectionFactory(databasePath);
            var viewModel = new MainViewModel(new SqliteCatalogRepository(factory), new SqliteProjectRepository(factory), new DialogService());
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не вдалося запустити програму:\n" + ex.Message, "Системи опалення", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Неочікувана помилка:\n" + e.Exception.Message, "Системи опалення", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
