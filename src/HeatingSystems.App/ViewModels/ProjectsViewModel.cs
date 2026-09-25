using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.App.Services;
using HeatingSystems.Core.Abstractions;

namespace HeatingSystems.App.ViewModels;

/// <summary>Saved projects (page "Проєкти").</summary>
public sealed partial class ProjectsViewModel : PageViewModel
{
    private readonly IProjectRepository _projects;
    private readonly IDialogService _dialogs;

    public ProjectsViewModel(IProjectRepository projects, IDialogService dialogs)
        : base("Проєкти", "\uE8B7", "Збережені розрахунки")
    {
        _projects = projects;
        _dialogs = dialogs;
        Refresh();
    }

    /// <summary>Raised when the user opens a project.</summary>
    public event EventHandler<ProjectSummary>? OpenRequested;

    public ObservableCollection<ProjectSummary> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand), nameof(DeleteCommand))]
    private ProjectSummary? _selected;

    [ObservableProperty] private bool _isEmpty;

    public void Refresh()
    {
        Items.Clear();
        foreach (var p in _projects.ListProjects()) Items.Add(p);
        IsEmpty = Items.Count == 0;
    }

    private bool HasSelection() => Selected is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Open()
    {
        if (Selected is { } p) OpenRequested?.Invoke(this, p);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is not { } p) return;
        if (!_dialogs.Confirm($"Видалити проєкт «{p.Name}»?")) return;
        _projects.DeleteProject(p.Id);
        Refresh();
    }
}
