using System.Globalization;
using System.Windows;
using System.Windows.Data;

using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.Converters;

/// <summary>
/// Two-way number converter for text boxes: formats with the UI culture and accepts both ',' and '.'
/// as decimal separator. Invalid input raises a validation error on the binding.
/// </summary>
public sealed class NumberConverter : IValueConverter
{
    /// <summary>Numeric format string, e.g. "N2". Empty → shortest round-trip representation.</summary>
    public string Format { get; set; } = "";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        double d when double.IsNaN(d) => "",
        double d => string.IsNullOrEmpty(Format) ? d.ToString("0.###", culture) : d.ToString(Format, culture),
        int i => i.ToString(string.IsNullOrEmpty(Format) ? "0" : Format, culture),
        null => "",
        _ => System.Convert.ToString(value, culture) ?? "",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = (value as string ?? "").Trim().Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
        var target = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (text.Length == 0)
        {
            if (Nullable.GetUnderlyingType(targetType) is not null) return null!;
            throw new FormatException(Localizer.T("input.enterNumber"));
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new FormatException(Localizer.T("input.invalidNumber"));
        if (target == typeof(int))
        {
            if (number != Math.Floor(number) || number is < int.MinValue or > int.MaxValue)
                throw new FormatException(Localizer.T("input.enterInteger"));
            return (int)number;
        }
        return number;
    }
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
