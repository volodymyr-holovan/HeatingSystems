using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.App.Services;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Models;

using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.ViewModels;

/// <summary>Editable tariff row.</summary>
public sealed partial class CarrierRow(EnergyCarrier carrier) : ObservableObject
{
    public EnergyCarrier Carrier { get; } = carrier;
    public string Code => Carrier.Code;
    public string Name => Carrier.Name;
    public string Unit => Carrier.Unit;
    public double EnergyPerUnit => Carrier.EnergyPerUnit;
    public string Source => Carrier.Source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PricePerKWh), nameof(IsModified))]
    private double _price = carrier.PricePerUnit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    private double _co2 = carrier.Co2PerKWh;

    public double PricePerKWh => EnergyPerUnit > 0 ? Price / EnergyPerUnit : 0;

    public bool IsModified => Math.Abs(Price - Carrier.PricePerUnit) > 1e-9 || Math.Abs(Co2 - Carrier.Co2PerKWh) > 1e-9;
}

public sealed record ClimateRow(string City, string Region, double DesignTemperature, int Days, double MeanTemperature,
    double AnnualMean, double DegreeDays, int Zone, string Source);

public sealed record MaterialRow(string Name, string Category, double Conductivity, double Density, string Source);

/// <summary>Reference data: tariffs, climate, materials (page "Reference data").</summary>
public sealed partial class ReferenceDataViewModel : PageViewModel
{
    private readonly ICatalogRepository _catalog;
    private readonly IDialogService _dialogs;

    public ReferenceDataViewModel(ICatalogRepository catalog, IDialogService dialogs)
        : base("page.reference", "\uE8A5", "page.reference.subtitle")
    {
        _catalog = catalog;
        _dialogs = dialogs;
        Reload();
    }

    /// <summary>Raised after tariffs were saved, so results can be recalculated.</summary>
    public event EventHandler? TariffsChanged;

    public ObservableCollection<CarrierRow> Carriers { get; } = new();
    public ObservableCollection<ClimateRow> Climates { get; } = new();
    public ObservableCollection<MaterialRow> Materials { get; } = new();
    public ObservableCollection<WindowType> Windows { get; } = new();
    public ObservableCollection<HeatingTechnology> Technologies { get; } = new();

    [ObservableProperty] private string _catalogSource = "";

    public void Reload()
    {
        Carriers.Clear();
        foreach (var c in _catalog.GetEnergyCarriers()) Carriers.Add(new CarrierRow(c));
        Climates.Clear();
        foreach (var c in _catalog.GetClimateLocations())
            Climates.Add(new ClimateRow(c.City, c.Region, c.DesignTemperature, c.HeatingSeasonDays, c.HeatingSeasonMeanTemperature,
                c.AnnualMeanTemperature, c.DegreeDays(20), c.ClimateZone, c.Source));
        Materials.Clear();
        foreach (var m in _catalog.GetMaterials())
            Materials.Add(new MaterialRow(m.Name, DisplayNames.Category(m.Category), m.Conductivity, m.Density, m.Source));
        Windows.Clear();
        foreach (var w in _catalog.GetWindowTypes()) Windows.Add(w);
        Technologies.Clear();
        foreach (var t in _catalog.GetHeatingTechnologies()) Technologies.Add(t);
        var s = _catalog.GetStatistics();
        CatalogSource = Localizer.F("reference.catalogSource", s.DataSource, s.HeatPumpCount, s.ManufacturerCount);
    }

    [RelayCommand]
    private void SaveTariffs()
    {
        var changed = Carriers.Where(c => c.IsModified).ToList();
        if (changed.Count == 0)
        {
            _dialogs.ShowInfo(Localizer.T("reference.noChanges"));
            return;
        }
        if (changed.Any(c => c.Price < 0 || c.Co2 < 0))
        {
            _dialogs.ShowError(Localizer.T("reference.negative"));
            return;
        }
        foreach (var c in changed) _catalog.UpdateEnergyCarrier(c.Code, c.Price, c.Co2);
        Reload();
        TariffsChanged?.Invoke(this, EventArgs.Empty);
        _dialogs.ShowInfo(Localizer.F("reference.saved", changed.Count));
    }

    [RelayCommand]
    private void DiscardTariffs() => Reload();

    public override void RefreshLanguage()
    {
        Reload();
        base.RefreshLanguage();
    }
}
