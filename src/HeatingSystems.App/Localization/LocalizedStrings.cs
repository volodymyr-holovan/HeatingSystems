using System.ComponentModel;
using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.Localization;

/// <summary>
/// Bindable facade over <see cref="Localizer"/>: XAML binds to the indexer and is refreshed
/// automatically when the language changes.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    public static LocalizedStrings Instance { get; } = new();

    private LocalizedStrings() =>
        Localizer.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public string this[string key] => Localizer.T(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
