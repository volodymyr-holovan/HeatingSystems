using System.IO;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using HeatingSystems.App.Services;
using HeatingSystems.App.ViewModels;
using HeatingSystems.App.Views;
using System.Windows.Markup;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Projects;
using HeatingSystems.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeatingSystems.App.Tests;

internal sealed class FakeDialogs : IDialogService
{
    public List<string> Messages { get; } = new();
    public void ShowError(string message) => Messages.Add("error: " + message);
    public void ShowInfo(string message) => Messages.Add("info: " + message);
    public bool Confirm(string message) => true;
    public string? SaveFile(string defaultFileName, string filter) => null;
    public void OpenWithShell(string path) { }
}

/// <summary>Collects WPF data binding errors (System.Windows.Data Error …).</summary>
internal sealed class BindingErrorListener : TraceListener
{
    public List<string> Errors { get; } = new();
    public override void Write(string? message) { }
    public override void WriteLine(string? message)
    {
        if (message is not null) Errors.Add(message);
    }
}

/// <summary>
/// Opens the real main window with the real database, visits every page with and without results and fails on
/// XAML parse errors, exceptions and data binding errors.
/// </summary>
public class UiSmokeTests
{
    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
    }

    private static void Flush() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    [Fact]
    public void MainWindow_RendersAllPages_WithoutBindingErrors()
    {
        RunSta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "HeatingSystemsUiTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var listener = new BindingErrorListener();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                var app = new App();
                app.InitializeComponent();

                var bundled = Path.Combine(AppContext.BaseDirectory, DatabaseInitializer.CatalogFileName);
                Assert.True(File.Exists(bundled), "Catalogue must be copied next to the application.");
                var dbPath = DatabaseInitializer.EnsureUserDatabase(bundled, Path.Combine(directory, "ui.db"));
                var factory = new SqliteConnectionFactory(dbPath);
                var catalog = new SqliteCatalogRepository(factory);
                var settings = new SqliteSettingsRepository(factory);
                var dialogs = new FakeDialogs();
                Localizer.SetLanguage(AppLanguage.English);

                MainWindow? window = null;
                // Mirrors App: the window formatting language is switched before the pages refresh.
                EventHandler onLanguage = (_, _) =>
                {
                    if (window is not null) window.Language = XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
                };
                Localizer.LanguageChanged += onLanguage;
                var vm = new MainViewModel(catalog, new SqliteProjectRepository(factory), settings, dialogs);

                window = new MainWindow { DataContext = vm, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000 };
                window.Show();
                Flush();

                void VisitAllPages()
                {
                    foreach (var page in vm.Pages)
                    {
                        vm.SelectedPage = page;
                        Flush();
                        window.UpdateLayout();
                        Flush();
                    }
                }

                // 1) Empty state.
                VisitAllPages();

                // 2) With results.
                Assert.Empty(vm.Building.ValidationErrors);
                var outcome = new CalculationService(catalog).Calculate(vm.Building.ToBuildingInput(), vm.Recommendation.BuildOptions());
                vm.ApplyOutcome(outcome);
                Assert.True(vm.Results.HasResults);
                Assert.NotEmpty(vm.Comparison.Rows);
                Assert.NotEmpty(vm.Recommendation.Rows);
                VisitAllPages();

                // 3) Catalogue paging and project round trip through the UI view models.
                vm.Catalog.NextPageCommand.Execute(null);
                vm.Catalog.SearchText = "Vaillant";
                vm.Catalog.SearchCommand.Execute(null);
                Assert.True(vm.Catalog.TotalCount > 0);
                vm.ProjectName = "Smoke test";
                vm.SaveProjectCommand.Execute(null);
                Assert.NotNull(vm.ProjectId);
                vm.Projects.Refresh();
                Assert.Contains(vm.Projects.Items, p => p.Name == "Smoke test");
                var data = ProjectData.FromJson(vm.Building.ToProjectData().ToJson());
                vm.Building.ResetToDefaults();
                vm.Building.HeatedFloorArea = 55;
                vm.Building.Load(data);
                Assert.Equal(120, vm.Building.HeatedFloorArea);
                VisitAllPages();

                // 4) Switch to Ukrainian through the UI view model, visit everything, switch back.
                var selectedClimate = vm.Building.SelectedClimate;
                vm.SelectedLanguage = vm.Languages.Single(l => l.Value == AppLanguage.Ukrainian);
                Assert.Equal(AppLanguage.Ukrainian, Localizer.Language);
                Assert.Equal("Ukrainian", settings.Get(MainViewModel.LanguageSettingKey));
                Assert.Same(selectedClimate, vm.Building.SelectedClimate);
                Assert.Equal("Будівля", vm.Building.Title);
                Assert.Equal("uk-UA", window.Language.IetfLanguageTag, ignoreCase: true);
                VisitAllPages();
                vm.SelectedLanguage = vm.Languages.Single(l => l.Value == AppLanguage.English);
                Assert.Equal("Building", vm.Building.Title);
                VisitAllPages();
                Localizer.LanguageChanged -= onLanguage;

                window.Close();
                Assert.DoesNotContain(dialogs.Messages, m => m.StartsWith("error", StringComparison.Ordinal));
                Assert.True(listener.Errors.Count == 0, "Binding errors:\n" + string.Join("\n", listener.Errors.Distinct()));
                Assert.True(Localizer.MissingKeys.Count == 0, "Missing translations: " + string.Join(", ", Localizer.MissingKeys));
            }
            finally
            {
                Localizer.SetLanguage(AppLanguage.English);
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                SqliteConnection.ClearAllPools();
                Application.Current?.Shutdown();
                try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
            }
        });
    }
}
