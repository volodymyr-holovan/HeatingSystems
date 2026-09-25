using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace HeatingSystems.App.Services;

public sealed class DialogService : IDialogService
{
    private const string Caption = "Системи опалення";

    private static Window? Owner => Application.Current?.MainWindow;

    public void ShowError(string message) => Show(message, MessageBoxImage.Error);

    public void ShowInfo(string message) => Show(message, MessageBoxImage.Information);

    public bool Confirm(string message) =>
        (Owner is { } owner
            ? MessageBox.Show(owner, message, Caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(message, Caption, MessageBoxButton.YesNo, MessageBoxImage.Question)) == MessageBoxResult.Yes;

    public string? SaveFile(string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog { FileName = defaultFileName, Filter = filter, AddExtension = true, OverwritePrompt = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No associated application — the file is saved anyway.
        }
    }

    private static void Show(string message, MessageBoxImage image)
    {
        if (Owner is { } owner) MessageBox.Show(owner, message, Caption, MessageBoxButton.OK, image);
        else MessageBox.Show(message, Caption, MessageBoxButton.OK, image);
    }
}
