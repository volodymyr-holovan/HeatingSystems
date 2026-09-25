using System.Windows.Data;
using System.Windows.Markup;

namespace HeatingSystems.App.Localization;

/// <summary>Markup extension <c>{l:Tr key}</c>: a live-updating binding to a localised string.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = LocalizedStrings.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
