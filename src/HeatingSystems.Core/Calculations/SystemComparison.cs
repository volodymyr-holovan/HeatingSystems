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
public sealed record SystemOption(
    string Name,
    SystemKind Kind,
    EnergyCarrier Carrier,
    double Efficiency,
    double FinalEnergy,
    double FuelQuantity,
    double AnnualCost,
    double AnnualCo2,
    string Notes)
{
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
        return Build(tech.Name, kind, carrier, eff, final, tech.Description);
    }

    public static SystemOption Evaluate(HeatPumpSeasonalResult hp, EnergyCarrier electricity)
    {
        var notes = hp.Coverage < 0.999
            ? $"Резервний ТЕН покриває {(1 - hp.Coverage) * 100:0.#} % опалення"
            : "Покриває все навантаження без резервного ТЕНа";
        return Build($"Тепловий насос: {hp.HeatPump.DisplayName}", SystemKind.HeatPump, electricity,
            hp.OverallSpf, hp.TotalElectricity, notes);
    }

    private static SystemOption Build(string name, SystemKind kind, EnergyCarrier carrier, double eff, double final, string notes) =>
        new(name, kind, carrier, eff, final,
            carrier.EnergyPerUnit > 0 ? final / carrier.EnergyPerUnit : 0,
            final * carrier.PricePerKWh,
            final * carrier.Co2PerKWh,
            notes);

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
