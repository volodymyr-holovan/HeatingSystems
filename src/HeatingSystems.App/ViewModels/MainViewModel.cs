using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.App.Services;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Projects;
using HeatingSystems.Core.Reports;

namespace HeatingSystems.App.ViewModels;

/// <summary>Shell: navigation, calculation, projects and report export.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ICatalogRepository _catalog;
    private readonly IProjectRepository _projects;
    private readonly IDialogService _dialogs;
    private readonly ISettingsRepository _settings;
    private readonly CalculationService _calculation;

    public const string LanguageSettingKey = "ui.language";

    public MainViewModel(ICatalogRepository catalog, IProjectRepository projects, ISettingsRepository settings, IDialogService dialogs)
    {
        _catalog = catalog;
        _projects = projects;
        _dialogs = dialogs;
        _settings = settings;
        _calculation = new CalculationService(catalog);
        _projectName = Localizer.T("project.newName");
        _status = Localizer.T("status.ready");
        _selectedLanguage = DisplayNames.Languages.First(l => l.Value == Localizer.Language);

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
        Localizer.LanguageChanged += (_, _) => OnLanguageChanged();
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
    [ObservableProperty] private string _projectName;
    [ObservableProperty] private int? _projectId;
    [ObservableProperty] private string _status;
    [ObservableProperty] private Option<AppLanguage> _selectedLanguage;

    public IReadOnlyList<Option<AppLanguage>> Languages => DisplayNames.Languages;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private bool _isBusy;

    public string WindowTitle => $"{Localizer.T("app.title")} — {ProjectName}";

    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnSelectedLanguageChanged(Option<AppLanguage> value)
    {
        if (value is null) return;
        _settings.Set(LanguageSettingKey, value.Value.ToString());
        Localizer.SetLanguage(value.Value);
    }

    private void OnLanguageChanged()
    {
        foreach (var page in Pages) page.RefreshLanguage();
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(SelectedPage));
        Status = Localizer.T("status.languageChanged");
    }

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
            _dialogs.ShowError(Localizer.T("status.checkInput") + "\n• " + string.Join("\n• ", errors));
            SelectedPage = Building;
            return;
        }

        IsBusy = true;
        Status = Localizer.T("status.calculating");
        try
        {
            var input = Building.ToBuildingInput();
            var options = Recommendation.BuildOptions();
            var outcome = await Task.Run(() => _calculation.Calculate(input, options));
            ApplyOutcome(outcome);
            Status = Localizer.F("status.calculated", outcome.HeatLoss.DesignHeatLoad / 1000, outcome.Demand.SpaceHeating,
                outcome.Demand.SpecificSpaceHeating);
            if (SelectedPage == Building) SelectedPage = Results;
        }
        catch (Exception ex)
        {
            Status = Localizer.T("status.calculationFailed");
            _dialogs.ShowError(Localizer.F("error.calculation", ex.Message));
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
        ProjectName = Localizer.T("project.newName");
        ApplyOutcome(null);
        SelectedPage = Building;
        Status = Localizer.T("status.newProject");
    }

    [RelayCommand]
    private void SaveProject() => Save(asNew: false);

    [RelayCommand]
    private void SaveProjectAs() => Save(asNew: true);

    private void Save(bool asNew)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            _dialogs.ShowError(Localizer.T("error.projectName"));
            return;
        }
        if (Building.SelectedClimate is null)
        {
            _dialogs.ShowError(Localizer.T("validation.city"));
            return;
        }
        try
        {
            var json = Building.ToProjectData().ToJson();
            var o = Results.Outcome;
            ProjectId = _projects.SaveProject(ProjectName, json, Building.SelectedClimate.City,
                o?.HeatLoss.DesignHeatLoad ?? 0, o?.Demand.SpaceHeating ?? 0, asNew ? null : ProjectId);
            Projects.Refresh();
            Status = Localizer.F("status.saved", ProjectName);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Localizer.F("error.save", ex.Message));
        }
    }

    private async void OpenProject(ProjectSummary summary)
    {
        try
        {
            var json = _projects.LoadProjectPayload(summary.Id) ?? throw new InvalidOperationException(Localizer.T("error.projectNotFound"));
            Building.Load(ProjectData.FromJson(json));
            ProjectId = summary.Id;
            ProjectName = summary.Name;
            ApplyOutcome(null);
            Status = Localizer.F("status.opened", summary.Name);
            SelectedPage = Building;
            await CalculateAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Localizer.F("error.open", ex.Message));
        }
    }

    [RelayCommand]
    private void ExportReport()
    {
        if (Results.Outcome is not { } outcome)
        {
            _dialogs.ShowInfo(Localizer.T("status.calculateFirst"));
            return;
        }
        var safeName = string.Concat(ProjectName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = _dialogs.SaveFile(Localizer.F("report.fileName", safeName), Localizer.T("report.fileFilter"));
        if (path is null) return;
        try
        {
            var html = HtmlReportBuilder.Build(outcome, ProjectName, _catalog.GetStatistics().DataSource);
            File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Status = Localizer.F("status.reportSaved", path);
            _dialogs.OpenWithShell(path);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Localizer.F("error.report", ex.Message));
        }
    }
}
