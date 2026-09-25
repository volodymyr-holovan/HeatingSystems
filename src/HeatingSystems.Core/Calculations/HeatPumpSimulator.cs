using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>Operating envelope of a heat pump at given source and flow temperatures.</summary>
/// <param name="Cop">Coefficient of performance, –.</param>
/// <param name="MaxThermalPower">Maximum heat output, W.</param>
/// <param name="MinThermalPower">Minimum continuous heat output (below it the unit cycles), W.</param>
public readonly record struct HeatPumpOperatingPoint(double Cop, double MaxThermalPower, double MinThermalPower)
{
    public bool IsAvailable => Cop > 1.0 && MaxThermalPower > 0;
}

/// <summary>Seasonal simulation result of a heat pump in a given building.</summary>
public sealed class HeatPumpSeasonalResult
{
    public required HeatPumpModel HeatPump { get; init; }

    /// <summary>Space heating supplied by the heat pump compressor, kWh/a.</summary>
    public required double HeatFromHeatPump { get; init; }
    /// <summary>Space heating supplied by the electric back-up heater, kWh/a.</summary>
    public required double HeatFromBackup { get; init; }
    /// <summary>Electricity used for space heating (compressor + back-up), kWh/a.</summary>
    public required double SpaceHeatingElectricity { get; init; }

    public required double HotWaterHeat { get; init; }
    public required double HotWaterElectricity { get; init; }

    /// <summary>Heat pump heat output at design outdoor temperature and design flow temperature, W.</summary>
    public required double CapacityAtDesign { get; init; }
    /// <summary>COP at design conditions, –.</summary>
    public required double CopAtDesign { get; init; }
    /// <summary>Bivalent temperature: below it the heat pump alone cannot cover the load, °C (null if never).</summary>
    public required double? BivalentTemperature { get; init; }
    /// <summary>Hours per year with back-up heater operation, h.</summary>
    public required double BackupHours { get; init; }
    public required double DesignHeatLoad { get; init; }

    public double SpaceHeating => HeatFromHeatPump + HeatFromBackup;
    public double TotalHeat => SpaceHeating + HotWaterHeat;
    public double TotalElectricity => SpaceHeatingElectricity + HotWaterElectricity;

    /// <summary>Seasonal performance factor for space heating on site (SCOP_on), –.</summary>
    public double SpaceHeatingSpf => SpaceHeatingElectricity > 0 ? SpaceHeating / SpaceHeatingElectricity : 0;

    /// <summary>Overall seasonal performance factor including DHW, –.</summary>
    public double OverallSpf => TotalElectricity > 0 ? TotalHeat / TotalElectricity : 0;

    /// <summary>Share of space heating covered by the compressor, –.</summary>
    public double Coverage => SpaceHeating > 0 ? HeatFromHeatPump / SpaceHeating : 1;

    /// <summary>Capacity at design conditions divided by the design heat load, –.</summary>
    public double SizingRatio => DesignHeatLoad > 0 ? CapacityAtDesign / DesignHeatLoad : 0;
}

/// <summary>
/// Seasonal heat pump simulation using the hplib performance model (Schwarz et al., KEYMARK regression)
/// and the EN 14825 bin method with part-load degradation.
/// </summary>
public static class HeatPumpSimulator
{
    /// <summary>Degradation coefficient C_d for cycling (EN 14825 default).</summary>
    public const double CyclingDegradation = 0.9;

    /// <summary>Minimum modulation of regulated units as a share of full electrical input (hplib).</summary>
    public const double MinimumModulation = 0.25;

    /// <summary>Brine inlet temperature of ground-source units (EN 14825 reference B0), °C.</summary>
    public const double BrineTemperature = 0.0;

    /// <summary>Ground water inlet temperature of water/water units (EN 14825 reference W10), °C.</summary>
    public const double GroundWaterTemperature = 10.0;

    /// <summary>Efficiency of the electric back-up heater, –.</summary>
    public const double BackupEfficiency = 1.0;

    /// <summary>Source-side inlet temperature for the given outdoor temperature, °C.</summary>
    public static double SourceTemperature(HeatSource source, double outdoorTemperature) => source switch
    {
        HeatSource.Air => outdoorTemperature,
        HeatSource.Brine => BrineTemperature,
        HeatSource.Water => GroundWaterTemperature,
        _ => outdoorTemperature,
    };

    /// <summary>
    /// Operating envelope for an outdoor temperature and a flow (output) temperature, following hplib:
    /// COP = p1·T_in + p2·T_out + p3 + p4·T_amb.
    /// Regulated units: the fitted P_el describes load-following operation at the EN 14825 test points, therefore
    /// the maximum electrical input is limited by P_el,ref and the minimum is 25 % of P_el at the reference source
    /// temperature (T_in = T_amb = −7 °C for air, T_amb = −7 °C for brine).
    /// On/off units: P_el = P_el,ref·(k1·T_in + k2·T_out + k3 + k4·T_amb) is the full-load input.
    /// </summary>
    public static HeatPumpOperatingPoint OperatingPoint(HeatPumpModel hp, double outdoorTemperature, double flowTemperature)
    {
        if (flowTemperature > hp.MaxFlowTemperature + 1e-9) return default;
        if (hp.Source == HeatSource.Air && hp.OperationLimitTemperature is { } tol && outdoorTemperature < tol) return default;

        var tIn = SourceTemperature(hp.Source, outdoorTemperature);
        var tAmb = outdoorTemperature;
        var cop = hp.CopP1 * tIn + hp.CopP2 * flowTemperature + hp.CopP3 + hp.CopP4 * tAmb;
        if (!(cop > 1.0)) return default;

        double maxElectric, minElectric;
        if (hp.Control == CompressorControl.Regulated)
        {
            var (refIn, refAmb) = hp.Source switch
            {
                HeatSource.Air => (-7.0, -7.0),
                HeatSource.Brine => (tIn, -7.0),
                _ => (tIn, tAmb),
            };
            maxElectric = hp.ElectricPowerRef;
            minElectric = MinimumModulation * ElectricPower(hp, refIn, flowTemperature, refAmb);
        }
        else
        {
            maxElectric = ElectricPower(hp, tIn, flowTemperature, tAmb);
            minElectric = maxElectric;
        }

        if (!(maxElectric > 0)) return default;
        minElectric = Math.Clamp(minElectric, 0, maxElectric);
        return new HeatPumpOperatingPoint(cop, maxElectric * cop, minElectric * cop);
    }

    private static double ElectricPower(HeatPumpModel hp, double tIn, double tOut, double tAmb) =>
        hp.ElectricPowerRef * (hp.PelP1 * tIn + hp.PelP2 * tOut + hp.PelP3 + hp.PelP4 * tAmb);

    /// <summary>
    /// Heat supplied by the unit for a bin load and the electricity it needs, including cycling losses below the
    /// minimum output (EN 14825: COP_bin = COP·CR / (C_d·CR + 1 − C_d)).
    /// </summary>
    public static (double Heat, double Electricity) Operate(HeatPumpOperatingPoint op, double load)
    {
        if (!op.IsAvailable || load <= 0) return (0, 0);
        var heat = Math.Min(load, op.MaxThermalPower);
        var cop = op.Cop;
        if (heat < op.MinThermalPower)
        {
            var cr = heat / op.MinThermalPower;
            cop = op.Cop * cr / (CyclingDegradation * cr + 1 - CyclingDegradation);
        }
        return (heat, heat / cop);
    }

    public static HeatPumpSeasonalResult Simulate(HeatPumpModel hp, BuildingInput building, HeatLossResult loss, EnergyDemandResult demand)
    {
        double qHp = 0, qBackup = 0, eHp = 0, eBackup = 0, backupHours = 0;
        double? bivalent = null;

        foreach (var bin in demand.Bins)
        {
            if (bin.Load <= 0) continue;
            var op = OperatingPoint(hp, bin.OutdoorTemperature, bin.FlowTemperature);
            var (hpHeat, hpElectric) = Operate(op, bin.Load);

            var backup = bin.Load - hpHeat;
            if (backup > 1e-6)
            {
                backupHours += bin.Hours;
                bivalent = bivalent is null ? bin.OutdoorTemperature : Math.Max(bivalent.Value, bin.OutdoorTemperature);
            }

            qHp += hpHeat * bin.Hours;
            eHp += hpElectric * bin.Hours;
            qBackup += backup * bin.Hours;
            eBackup += backup / BackupEfficiency * bin.Hours;
        }

        var design = OperatingPoint(hp, building.Climate.DesignTemperature, building.DesignFlowTemperature);
        var (dhwHeat, dhwElectric) = HotWater(hp, building, demand.HotWater);

        return new HeatPumpSeasonalResult
        {
            HeatPump = hp,
            HeatFromHeatPump = qHp / 1000,
            HeatFromBackup = qBackup / 1000,
            SpaceHeatingElectricity = (eHp + eBackup) / 1000,
            HotWaterHeat = dhwHeat,
            HotWaterElectricity = dhwElectric,
            CapacityAtDesign = design.IsAvailable ? design.MaxThermalPower : 0,
            CopAtDesign = design.IsAvailable ? design.Cop : 0,
            BivalentTemperature = bivalent,
            BackupHours = backupHours,
            DesignHeatLoad = loss.DesignHeatLoad,
        };
    }

    /// <summary>
    /// DHW heating by the heat pump at the annual mean outdoor temperature. If the heat pump cannot reach the
    /// required outlet temperature, the remaining temperature lift is provided by the electric heater.
    /// </summary>
    private static (double Heat, double Electricity) HotWater(HeatPumpModel hp, BuildingInput b, double demand)
    {
        if (demand <= 0) return (0, 0);
        var required = b.HotWaterTemperature + 5.0;
        var outlet = Math.Min(required, hp.MaxFlowTemperature);
        var share = Math.Clamp((outlet - 5.0 - b.ColdWaterTemperature) / (b.HotWaterTemperature - b.ColdWaterTemperature), 0, 1);
        var op = OperatingPoint(hp, b.Climate.AnnualMeanTemperature, outlet);
        if (!op.IsAvailable) share = 0;
        var electricity = demand * share / (share > 0 ? op.Cop : 1) + demand * (1 - share) / BackupEfficiency;
        return (demand, electricity);
    }
}
