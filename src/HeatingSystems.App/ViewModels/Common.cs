using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.ViewModels;

/// <summary>Value with a localised display name for combo boxes; the name follows the UI language.</summary>
public sealed class Option<T> : INotifyPropertyChanged
{
    public Option(T value, string nameKey)
    {
        Value = value;
        NameKey = nameKey;
        Localizer.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
    }

    public T Value { get; }
    public string NameKey { get; }
    public string Name => Localizer.T(NameKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => Name;
}

/// <summary>Base class of the pages shown in the navigation sidebar.</summary>
public abstract class PageViewModel(string titleKey, string glyph, string subtitleKey) : ObservableObject
{
    public string Title => Localizer.T(titleKey);

    /// <summary>Segoe MDL2 Assets / Segoe Fluent Icons glyph.</summary>
    public string Glyph { get; } = glyph;

    public string Subtitle => Localizer.T(subtitleKey);

    /// <summary>Re-evaluates all bindings after the UI language changed.</summary>
    public virtual void RefreshLanguage() => OnPropertyChanged(string.Empty);
}

/// <summary>One bar of a horizontal or vertical bar chart.</summary>
/// <param name="Fraction">Bar length relative to the largest bar, 0…1.</param>
/// <param name="Kind">Free category used by the view for colouring.</param>
public sealed record BarItem(string Label, double Value, double Fraction, string ValueText, string Kind = "")
{
    /// <summary>Bar height for vertical charts, px.</summary>
    public double Height => Math.Max(1, Fraction * 160);
}
