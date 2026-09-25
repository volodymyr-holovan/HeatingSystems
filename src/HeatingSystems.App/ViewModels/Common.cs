using CommunityToolkit.Mvvm.ComponentModel;

namespace HeatingSystems.App.ViewModels;

/// <summary>Value with a Ukrainian display name for combo boxes.</summary>
public sealed record Option<T>(T Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Base class of the pages shown in the navigation sidebar.</summary>
public abstract class PageViewModel(string title, string glyph, string subtitle) : ObservableObject
{
    public string Title { get; } = title;

    /// <summary>Segoe MDL2 Assets / Segoe Fluent Icons glyph.</summary>
    public string Glyph { get; } = glyph;

    public string Subtitle { get; } = subtitle;
}

/// <summary>One bar of a horizontal or vertical bar chart.</summary>
/// <param name="Fraction">Bar length relative to the largest bar, 0…1.</param>
/// <param name="Kind">Free category used by the view for colouring.</param>
public sealed record BarItem(string Label, double Value, double Fraction, string ValueText, string Kind = "")
{
    /// <summary>Bar height for vertical charts, px.</summary>
    public double Height => Math.Max(1, Fraction * 160);
}
