using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

public enum SystemKind
{
    Combustion,
    Electric,
    DistrictHeating,
    HeatPump,
}

/// <summary>Annual operation figures of one heating system option.</summary>
/// <param name="Efficiency">Seasonal efficiency or SPF (heat delivered / final energy), –.</param>
/// <param name="FinalEnergy">Final energy use, kWh/a.</param>
/// <param name="FuelQuantity">Fuel quantity in the carrier's unit, per year.</param>
/// <param name="AnnualCost">Annual energy cost, UAH.</param>
/// <param name="AnnualCo2">Annual CO₂ emissions, kg.</param>
/// <param name="Technology">Heat generator (null for heat pumps).</param>
/// <param name="HeatPump">Simulated heat pump (null for other generators).</param>
public sealed record SystemOption(
    SystemKind Kind,
    EnergyCarrier Carrier,
    double Efficiency,
    double FinalEnergy,
    double FuelQuantity,
    double AnnualCost,
    double AnnualCo2,
    HeatingTechnology? Technology,
    HeatPumpSeasonalResult? HeatPump)
{
    /// <summary>Display name in the current language.</summary>
    public string Name => HeatPump is { } hp
        ? Localizer.F("option.heatPump", hp.HeatPump.DisplayName)
        : Technology?.Name ?? "";

    /// <summary>Remark in the current language.</summary>
    public string Notes => HeatPump is { } hp
        ? hp.Coverage < 0.999
            ? Localizer.F("option.backupShare", (1 - hp.Coverage) * 100)
            : Localizer.T("option.noBackup")
        : Technology?.Description ?? "";

    public double CostPerKWhHeat(double heat) => heat > 0 ? AnnualCost / heat : 0;
}

/// <summary>Compares running costs and emissions of heating technologies for a building.</summary>
public static class SystemComparison
{
    public const string ElectricityCode = "electricity";

    public static SystemOption Evaluate(HeatingTechnology tech, EnergyCarrier carrier, EnergyDemandResult demand, double designFlowTemperature)
    {
        var eff = tech.EfficiencyAt(designFlowTemperature);
        var final = demand.Total / eff;
        var kind = carrier.Code switch
        {
            ElectricityCode => SystemKind.Electric,
            "district_heat" => SystemKind.DistrictHeating,
            _ => SystemKind.Combustion,
        };
        return Build(kind, carrier, eff, final, tech, null);
    }

    public static SystemOption Evaluate(HeatPumpSeasonalResult hp, EnergyCarrier electricity) =>
        Build(SystemKind.HeatPump, electricity, hp.OverallSpf, hp.TotalElectricity, null, hp);

    private static SystemOption Build(SystemKind kind, EnergyCarrier carrier, double eff, double final,
        HeatingTechnology? technology, HeatPumpSeasonalResult? heatPump) =>
        new(kind, carrier, eff, final,
            carrier.EnergyPerUnit > 0 ? final / carrier.EnergyPerUnit : 0,
            final * carrier.PricePerKWh,
            final * carrier.Co2PerKWh,
            technology,
            heatPump);

    public static IReadOnlyList<SystemOption> Compare(
        IEnumerable<HeatingTechnology> technologies,
        IReadOnlyDictionary<string, EnergyCarrier> carriers,
        EnergyDemandResult demand,
        double designFlowTemperature,
        IEnumerable<HeatPumpSeasonalResult> heatPumps)
    {
        var options = new List<SystemOption>();
        foreach (var t in technologies)
            if (carriers.TryGetValue(t.CarrierCode, out var c))
                options.Add(Evaluate(t, c, demand, designFlowTemperature));
        if (carriers.TryGetValue(ElectricityCode, out var el))
            options.AddRange(heatPumps.Select(hp => Evaluate(hp, el)));
        return options.OrderBy(o => o.AnnualCost).ToList();
    }
}
