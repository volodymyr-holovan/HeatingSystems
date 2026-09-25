using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Recommendations;

/// <summary>Constraints for heat pump selection.</summary>
public sealed record RecommendationOptions
{
    public IReadOnlyCollection<HeatSource> Sources { get; init; } = new[] { HeatSource.Air, HeatSource.Brine, HeatSource.Water };
    /// <summary>
    /// Minimum share of the annual space heating energy delivered by the compressor (mono-energetic operation,
    /// the rest by the electric back-up heater), –.
    /// </summary>
    public double MinCoverage { get; init; } = 0.95;
    /// <summary>Maximum ratio of the rated heat output P_design,h (35 °C) to the design heat load, –.</summary>
    public double MaxSizingRatio { get; init; } = 2.0;
    public double? MaxOutdoorSoundPower { get; init; }
    public bool NaturalRefrigerantsOnly { get; init; }
    public int Count { get; init; } = 20;
}

/// <param name="Variants">Other model names of the same manufacturer with identical certified performance data.</param>
public sealed record Recommendation(int Rank, HeatPumpSeasonalResult Result, double AnnualCost, double AnnualCo2, IReadOnlyList<string> Variants);

/// <summary>
/// Selects the heat pumps with the lowest annual running cost for a building.
/// Every candidate is simulated with the full bin method; a coarse SQL pre-filter on the reference power
/// keeps the evaluation set small.
/// </summary>
public sealed class HeatPumpRecommender(ICatalogRepository catalog)
{
    private static readonly HashSet<string> NaturalRefrigerants = new(StringComparer.OrdinalIgnoreCase)
        { "R290", "R744", "R717", "R600a", "R1270" };

    public IReadOnlyList<Recommendation> Recommend(
        BuildingInput building,
        HeatLossResult loss,
        EnergyDemandResult demand,
        EnergyCarrier electricity,
        RecommendationOptions options)
    {
        var load = loss.DesignHeatLoad;
        // Coarse SQL window on the reference output; the exact criteria are checked after simulation.
        var candidates = catalog.GetHeatPumpCandidates(load * 0.25, load * options.MaxSizingRatio * 2.5, options.Sources);

        var evaluated = candidates
            .AsParallel()
            .Where(hp => options.MaxOutdoorSoundPower is not { } maxNoise || (hp.SoundPowerOutdoor is { } n && n <= maxNoise))
            .Where(hp => !options.NaturalRefrigerantsOnly || NaturalRefrigerants.Contains(hp.Refrigerant))
            .Where(hp => hp.MaxFlowTemperature >= building.DesignFlowTemperature)
            .Where(hp => hp.RatedPowerLowTemp * 1000 <= options.MaxSizingRatio * load)
            .Select(hp => HeatPumpSimulator.Simulate(hp, building, loss, demand))
            .Where(r => r.Coverage >= options.MinCoverage)
            .Select(r => (Result: r, Cost: r.TotalElectricity * electricity.PricePerKWh, Co2: r.TotalElectricity * electricity.Co2PerKWh))
            .OrderBy(x => x.Cost)
            .ThenByDescending(x => x.Result.OverallSpf)
            .ThenBy(x => x.Result.HeatPump.Id)
            .ToList();

        // Manufacturers certify the same unit under several names (indoor module variants, voltages, brands).
        // Group identical performance data into one recommendation and list the other names as variants.
        return evaluated
            .GroupBy(x => PerformanceSignature(x.Result.HeatPump))
            .Select(g => (Best: g.First(), Variants: g.Skip(1).Select(v => v.Result.HeatPump.Model).Distinct()
                .Where(name => name != g.First().Result.HeatPump.Model).ToList()))
            .OrderBy(g => g.Best.Cost)
            .ThenByDescending(g => g.Best.Result.OverallSpf)
            .ThenBy(g => g.Best.Result.HeatPump.Id)
            .Take(options.Count)
            .Select((g, i) => new Recommendation(i + 1, g.Best.Result, g.Best.Cost, g.Best.Co2, g.Variants))
            .ToList();
    }

    private static (string, double, double, double, double, double, double) PerformanceSignature(HeatPumpModel hp) =>
        (hp.Manufacturer, Math.Round(hp.ElectricPowerRef), Math.Round(hp.ThermalPowerRef),
         Math.Round(hp.CopP1, 6), Math.Round(hp.CopP3, 6), Math.Round(hp.PelP3, 6), hp.Scop);
}
