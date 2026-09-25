using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Models;
using HeatingSystems.Core.Recommendations;

namespace HeatingSystems.Core.Calculations;

/// <summary>Everything calculated for one building.</summary>
public sealed class CalculationOutcome
{
    public required BuildingInput Building { get; init; }
    public required HeatLossResult HeatLoss { get; init; }
    public required EnergyDemandResult Demand { get; init; }
    public required IReadOnlyList<EnvelopeCheckItem> EnvelopeChecks { get; init; }
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
    public required IReadOnlyList<SystemOption> Options { get; init; }
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>Runs the complete calculation chain for a building.</summary>
public sealed class CalculationService(ICatalogRepository catalog)
{
    public CalculationOutcome Calculate(BuildingInput building, RecommendationOptions? options = null)
    {
        var loss = HeatLossCalculator.Calculate(building);
        var demand = EnergyDemandCalculator.Calculate(building, loss);
        var carriers = catalog.GetEnergyCarriers().ToDictionary(c => c.Code);
        var checks = EnvelopeCheck.Check(building, catalog.GetEnvelopeRequirements());

        IReadOnlyList<Recommendation> recommendations = Array.Empty<Recommendation>();
        if (carriers.TryGetValue(SystemComparison.ElectricityCode, out var electricity))
            recommendations = new HeatPumpRecommender(catalog).Recommend(building, loss, demand, electricity,
                options ?? new RecommendationOptions());

        // The comparison shows the best unit of every heat source type.
        var bestPerSource = recommendations
            .GroupBy(r => r.Result.HeatPump.Source)
            .Select(g => g.First().Result);

        var comparison = SystemComparison.Compare(catalog.GetHeatingTechnologies(), carriers, demand,
            building.DesignFlowTemperature, bestPerSource);

        return new CalculationOutcome
        {
            Building = building,
            HeatLoss = loss,
            Demand = demand,
            EnvelopeChecks = checks,
            Recommendations = recommendations,
            Options = comparison,
            CalculatedAt = DateTime.Now,
        };
    }
}
