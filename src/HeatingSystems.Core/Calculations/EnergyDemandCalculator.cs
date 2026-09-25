using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>Space heating load in a temperature bin.</summary>
/// <param name="Load">Net space heating load (after utilised gains), W.</param>
/// <param name="FlowTemperature">Required flow temperature, °C.</param>
public readonly record struct LoadBin(double OutdoorTemperature, double Hours, double Load, double FlowTemperature);

/// <summary>Annual energy need of the building.</summary>
public sealed class EnergyDemandResult
{
    public required IReadOnlyList<LoadBin> Bins { get; init; }

    /// <summary>Gross heat losses during the heating season, kWh.</summary>
    public required double HeatLosses { get; init; }

    /// <summary>Internal heat gains available during the heating season, kWh.</summary>
    public required double InternalGains { get; init; }

    /// <summary>Utilised internal gains, kWh.</summary>
    public required double UtilisedGains { get; init; }

    /// <summary>Net space heating energy need Q_H,nd, kWh/a.</summary>
    public required double SpaceHeating { get; init; }

    /// <summary>Domestic hot water energy need including storage/distribution losses, kWh/a.</summary>
    public required double HotWater { get; init; }

    public required double HeatedFloorArea { get; init; }

    public required double DegreeDays { get; init; }

    public double Total => SpaceHeating + HotWater;

    /// <summary>Specific space heating need, kWh/(m²·a).</summary>
    public double SpecificSpaceHeating => HeatedFloorArea > 0 ? SpaceHeating / HeatedFloorArea : 0;
}

/// <summary>
/// Annual heating energy need using a bin method with the gain utilisation factor of EN ISO 13790 (12.2.1.1).
/// </summary>
public static class EnergyDemandCalculator
{
    /// <summary>Reference time constant τ_H,0, h (EN ISO 13790, table 10).</summary>
    public const double ReferenceTimeConstant = 15.0;

    /// <summary>Internal heat capacity per m² of floor area κ_m, J/(m²·K) (EN ISO 13790, table 12).</summary>
    public static double HeatCapacityPerArea(ThermalMassClass mass) => mass switch
    {
        ThermalMassClass.Light => 110_000,
        ThermalMassClass.Medium => 165_000,
        ThermalMassClass.Heavy => 260_000,
        _ => 165_000,
    };

    public static EnergyDemandResult Calculate(BuildingInput b, HeatLossResult loss)
        => Calculate(b, loss, TemperatureBins.Create(b.Climate));

    public static EnergyDemandResult Calculate(BuildingInput b, HeatLossResult loss, IReadOnlyList<TemperatureBin> bins)
    {
        var climate = b.Climate;
        var ti = b.IndoorTemperature;
        var hAir = loss.TransmissionCoefficient + loss.VentilationCoefficient;

        // Ground: steady-state part relative to the annual mean + periodic part (EN ISO 13370).
        // Averaged over a heating season centred on the coldest period, the periodic term equals H_pe·(θ_m,e − θ_m,season).
        var groundLoss = loss.GroundCoefficient * (ti - climate.AnnualMeanTemperature)
                         + loss.GroundPeriodicCoefficient * Math.Max(0, climate.AnnualMeanTemperature - climate.HeatingSeasonMeanTemperature);

        var gains = b.InternalGains * b.HeatedFloorArea;
        var hTotal = hAir + loss.GroundCoefficient;
        var tau = hTotal > 0 ? HeatCapacityPerArea(b.ThermalMass) * b.HeatedFloorArea / 3600.0 / hTotal : 0;
        var a = 1.0 + tau / ReferenceTimeConstant;

        var loadBins = new List<LoadBin>(bins.Count);
        double lossesWh = 0, gainsWh = 0, utilisedWh = 0, needWh = 0;
        foreach (var bin in bins)
        {
            var losses = Math.Max(0, hAir * (ti - bin.Temperature) + groundLoss);
            var eta = GainUtilisation(gains, losses, a);
            var net = Math.Max(0, losses - eta * gains);
            var flow = HeatingCurve.FlowTemperature(bin.Temperature, ti, climate.DesignTemperature,
                b.DesignFlowTemperature, b.EmitterExponent);
            loadBins.Add(new LoadBin(bin.Temperature, bin.Hours, net, flow));

            lossesWh += losses * bin.Hours;
            gainsWh += gains * bin.Hours;
            utilisedWh += Math.Min(losses, eta * gains) * bin.Hours;
            needWh += net * bin.Hours;
        }

        return new EnergyDemandResult
        {
            Bins = loadBins,
            HeatLosses = lossesWh / 1000,
            InternalGains = gainsWh / 1000,
            UtilisedGains = utilisedWh / 1000,
            SpaceHeating = needWh / 1000,
            HotWater = b.IncludeHotWater ? HotWaterDemand(b) : 0,
            HeatedFloorArea = b.HeatedFloorArea,
            DegreeDays = climate.DegreeDays(ti),
        };
    }

    /// <summary>
    /// Gain utilisation factor η = (1 − γ^a)/(1 − γ^(a+1)), γ = Q_gn/Q_ht; η = a/(a+1) for γ = 1 (EN ISO 13790, 12.2.1.1).
    /// </summary>
    public static double GainUtilisation(double gains, double losses, double a)
    {
        if (gains <= 0) return 1.0;
        if (losses <= 0) return 0.0;
        var gamma = gains / losses;
        if (Math.Abs(gamma - 1.0) < 1e-9) return a / (a + 1.0);
        return (1.0 - Math.Pow(gamma, a)) / (1.0 - Math.Pow(gamma, a + 1.0));
    }

    /// <summary>
    /// Annual DHW energy need Q_W = N·V·365·ρc·(θ_hot − θ_cold)·(1 + f_loss), kWh/a,
    /// ρc = 1.163·10⁻³ kWh/(l·K).
    /// </summary>
    public static double HotWaterDemand(BuildingInput b) =>
        b.Occupants * b.HotWaterLitresPerPersonDay * 365.0 * 1.163e-3
        * (b.HotWaterTemperature - b.ColdWaterTemperature) * (1.0 + b.HotWaterLossFactor);
}
