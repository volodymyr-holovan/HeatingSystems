namespace HeatingSystems.App.Services;

/// <summary>User interaction that must not live in view models (message boxes, file dialogs, shell).</summary>
public interface IDialogService
{
    void ShowError(string message);
    void ShowInfo(string message);
    bool Confirm(string message);
    /// <summary>Asks for a target file; returns null when cancelled.</summary>
    string? SaveFile(string defaultFileName, string filter);
    void OpenWithShell(string path);
}
