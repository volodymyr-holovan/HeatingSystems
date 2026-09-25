using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.App.Services;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Projects;
using HeatingSystems.Core.Reports;

namespace HeatingSystems.App.ViewModels;

/// <summary>Shell: navigation, calculation, projects and report export.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ICatalogRepository _catalog;
    private readonly IProjectRepository _projects;
    private readonly IDialogService _dialogs;
    private readonly CalculationService _calculation;

    public MainViewModel(ICatalogRepository catalog, IProjectRepository projects, IDialogService dialogs)
    {
        _catalog = catalog;
        _projects = projects;
        _dialogs = dialogs;
        _calculation = new CalculationService(catalog);

        var reference = new ReferenceCache(catalog);
        Building = new BuildingViewModel(reference);
        Results = new ResultsViewModel();
        Comparison = new ComparisonViewModel();
        Recommendation = new RecommendationViewModel(catalog, dialogs);
        Catalog = new CatalogViewModel(catalog);
        ReferenceData = new ReferenceDataViewModel(catalog, dialogs);
        Projects = new ProjectsViewModel(projects, dialogs);

        Pages = new PageViewModel[] { Building, Results, Comparison, Recommendation, Catalog, ReferenceData, Projects };
        _selectedPage = Building;

        Projects.OpenRequested += (_, p) => OpenProject(p);
        ReferenceData.TariffsChanged += async (_, _) =>
        {
            if (Results.Outcome is not null) await CalculateAsync();
        };
    }

    public BuildingViewModel Building { get; }
    public ResultsViewModel Results { get; }
    public ComparisonViewModel Comparison { get; }
    public RecommendationViewModel Recommendation { get; }
    public CatalogViewModel Catalog { get; }
    public ReferenceDataViewModel ReferenceData { get; }
    public ProjectsViewModel Projects { get; }
    public IReadOnlyList<PageViewModel> Pages { get; }

    [ObservableProperty] private PageViewModel _selectedPage;
    [ObservableProperty] private string _projectName = "Новий проєкт";
    [ObservableProperty] private int? _projectId;
    [ObservableProperty] private string _status = "Готово. Заповніть дані будівлі та натисніть «Розрахувати».";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private bool _isBusy;

    public string WindowTitle => $"Системи опалення — {ProjectName}";

    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnSelectedPageChanged(PageViewModel value)
    {
        if (value == Catalog) Catalog.EnsureLoaded();
        if (value == Projects) Projects.Refresh();
    }

    private bool CanCalculate() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private async Task CalculateAsync()
    {
        var errors = Building.ValidationErrors;
        if (errors.Count > 0)
        {
            _dialogs.ShowError("Перевірте вихідні дані:\n• " + string.Join("\n• ", errors));
            SelectedPage = Building;
            return;
        }

        IsBusy = true;
        Status = "Розрахунок…";
        try
        {
            var input = Building.ToBuildingInput();
            var options = Recommendation.BuildOptions();
            var outcome = await Task.Run(() => _calculation.Calculate(input, options));
            ApplyOutcome(outcome);
            Status = $"Розраховано: Φ = {outcome.HeatLoss.DesignHeatLoad / 1000:0.00} кВт, " +
                     $"Q = {outcome.Demand.SpaceHeating:N0} кВт·год/рік ({outcome.Demand.SpecificSpaceHeating:0} кВт·год/м²).";
            if (SelectedPage == Building) SelectedPage = Results;
        }
        catch (Exception ex)
        {
            Status = "Помилка розрахунку.";
            _dialogs.ShowError("Помилка розрахунку: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void ApplyOutcome(CalculationOutcome? outcome)
    {
        Results.Outcome = outcome;
        Comparison.Update(outcome);
        Recommendation.Update(outcome);
    }

    [RelayCommand]
    private void NewProject()
    {
        Building.ResetToDefaults();
        ProjectId = null;
        ProjectName = "Новий проєкт";
        ApplyOutcome(null);
        SelectedPage = Building;
        Status = "Створено новий проєкт.";
    }

    [RelayCommand]
    private void SaveProject() => Save(asNew: false);

    [RelayCommand]
    private void SaveProjectAs() => Save(asNew: true);

    private void Save(bool asNew)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            _dialogs.ShowError("Введіть назву проєкту.");
            return;
        }
        if (Building.SelectedClimate is null)
        {
            _dialogs.ShowError("Оберіть місто.");
            return;
        }
        try
        {
            var json = Building.ToProjectData().ToJson();
            var o = Results.Outcome;
            ProjectId = _projects.SaveProject(ProjectName, json, Building.SelectedClimate.City,
                o?.HeatLoss.DesignHeatLoad ?? 0, o?.Demand.SpaceHeating ?? 0, asNew ? null : ProjectId);
            Projects.Refresh();
            Status = $"Проєкт «{ProjectName}» збережено.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Не вдалося зберегти проєкт: " + ex.Message);
        }
    }

    private async void OpenProject(ProjectSummary summary)
    {
        try
        {
            var json = _projects.LoadProjectPayload(summary.Id) ?? throw new InvalidOperationException("Проєкт не знайдено.");
            Building.Load(ProjectData.FromJson(json));
            ProjectId = summary.Id;
            ProjectName = summary.Name;
            ApplyOutcome(null);
            Status = $"Відкрито проєкт «{summary.Name}».";
            SelectedPage = Building;
            await CalculateAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Не вдалося відкрити проєкт: " + ex.Message);
        }
    }

    [RelayCommand]
    private void ExportReport()
    {
        if (Results.Outcome is not { } outcome)
        {
            _dialogs.ShowInfo("Спочатку виконайте розрахунок.");
            return;
        }
        var safeName = string.Concat(ProjectName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = _dialogs.SaveFile($"{safeName} — звіт.html", "Звіт HTML (*.html)|*.html");
        if (path is null) return;
        try
        {
            var html = HtmlReportBuilder.Build(outcome, ProjectName, _catalog.GetStatistics().DataSource);
            File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Status = $"Звіт збережено: {path}";
            _dialogs.OpenWithShell(path);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Не вдалося зберегти звіт: " + ex.Message);
        }
    }
}
