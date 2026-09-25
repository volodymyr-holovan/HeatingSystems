using System.Windows;
using System.Windows.Markup;
using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Number and date formatting of all bindings follows the UI language (updated by App on change).
        Language = XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
    }
}
