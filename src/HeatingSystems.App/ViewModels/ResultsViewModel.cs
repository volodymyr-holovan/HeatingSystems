using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeatingSystems.Core.Calculations;

using HeatingSystems.Core.Localization;

namespace HeatingSystems.App.ViewModels;

/// <summary>Heat load and energy demand results (page "Results").</summary>
public sealed partial class ResultsViewModel() : PageViewModel("page.results", "\uE9D2", "page.results.subtitle")
{
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private CalculationOutcome? _outcome;

    public ObservableCollection<BarItem> LossBars { get; } = new();
    public ObservableCollection<BarItem> BinBars { get; } = new();
    public ObservableCollection<EnvelopeCheckItem> EnvelopeChecks { get; } = new();

    public double DesignHeatLoadKw => (Outcome?.HeatLoss.DesignHeatLoad ?? 0) / 1000;
    public double SpecificHeatLoad => Outcome?.HeatLoss.SpecificHeatLoad ?? 0;
    public double SpaceHeating => Outcome?.Demand.SpaceHeating ?? 0;
    public double SpecificSpaceHeating => Outcome?.Demand.SpecificSpaceHeating ?? 0;
    public double HotWater => Outcome?.Demand.HotWater ?? 0;
    public double DegreeDays => Outcome?.Demand.DegreeDays ?? 0;
    public double HeatLosses => Outcome?.Demand.HeatLosses ?? 0;
    public double UtilisedGains => Outcome?.Demand.UtilisedGains ?? 0;
    public double TransmissionCoefficient => Outcome?.HeatLoss.TransmissionCoefficient ?? 0;
    public double VentilationCoefficient => Outcome?.HeatLoss.VentilationCoefficient ?? 0;
    public double EffectiveAirFlow => Outcome?.HeatLoss.EffectiveAirFlow ?? 0;
    public string Location => Outcome is { } o
        ? Localizer.F("results.location", o.Building.Climate.City, o.Building.Climate.DesignTemperature, o.Building.IndoorTemperature,
            o.Building.Climate.ClimateZone)
        : "";

    /// <summary>Energy efficiency hint by specific space heating need, kWh/(m²·a).</summary>
    public string EfficiencyLabel => SpecificSpaceHeating switch
    {
        <= 0 => "",
        < 30 => Localizer.T("efficiency.passive"),
        < 60 => Localizer.T("efficiency.low"),
        < 100 => Localizer.T("efficiency.moderate"),
        < 150 => Localizer.T("efficiency.elevated"),
        _ => Localizer.T("efficiency.high"),
    };

    public override void RefreshLanguage()
    {
        OnOutcomeChanged(Outcome);
        base.RefreshLanguage();
    }

    partial void OnOutcomeChanged(CalculationOutcome? value)
    {
        LossBars.Clear();
        BinBars.Clear();
        EnvelopeChecks.Clear();
        HasResults = value is not null;
        if (value is not null)
        {
            var total = value.HeatLoss.DesignHeatLoad;
            var max = value.HeatLoss.Items.Max(i => i.DesignLoss);
            foreach (var item in value.HeatLoss.Items.OrderByDescending(i => i.DesignLoss))
                LossBars.Add(new BarItem(item.Name, item.DesignLoss, max > 0 ? item.DesignLoss / max : 0,
                    Localizer.F("results.lossBar", item.DesignLoss, item.DesignLoss / total), item.Category.ToString()));

            // Annual space heating energy per 2 K outdoor temperature class.
            var classes = value.Demand.Bins
                .Where(b => b.Load > 0)
                .GroupBy(b => 2 * Math.Floor(b.OutdoorTemperature / 2))
                .OrderBy(g => g.Key)
                .Select(g => (Temperature: g.Key, Energy: g.Sum(b => b.Load * b.Hours) / 1000, Hours: g.Sum(b => b.Hours)))
                .ToList();
            var maxEnergy = classes.Count > 0 ? classes.Max(c => c.Energy) : 0;
            foreach (var c in classes)
                BinBars.Add(new BarItem($"{c.Temperature:0}", c.Energy, maxEnergy > 0 ? c.Energy / maxEnergy : 0,
                    Localizer.F("results.binBar", c.Temperature, c.Temperature + 2, c.Energy, c.Hours)));

            foreach (var check in value.EnvelopeChecks) EnvelopeChecks.Add(check);
        }

        foreach (var name in new[]
                 {
                     nameof(DesignHeatLoadKw), nameof(SpecificHeatLoad), nameof(SpaceHeating), nameof(SpecificSpaceHeating),
                     nameof(HotWater), nameof(DegreeDays), nameof(HeatLosses), nameof(UtilisedGains), nameof(TransmissionCoefficient),
                     nameof(VentilationCoefficient), nameof(EffectiveAirFlow), nameof(Location), nameof(EfficiencyLabel),
                 })
            OnPropertyChanged(name);
    }
}
