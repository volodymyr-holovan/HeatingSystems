using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>
/// Reproduction of the certified SCOP with the performance model: EN 14825 "average" reference heating season
/// (T_design = −10 °C, bivalent/part-load line through 16 °C), low temperature application with variable outlet.
/// Used to validate the regression model against the certified value of each product.
/// </summary>
public static class En14825
{
    public const double DesignTemperature = -10.0;

    /// <summary>Bin hours of the average reference heating season (EN 14825, table 7), Σ = 4910 h.</summary>
    public static IReadOnlyList<TemperatureBin> AverageClimateBins { get; } = new (double T, double H)[]
    {
        (-10, 1), (-9, 25), (-8, 23), (-7, 24), (-6, 27), (-5, 68), (-4, 91), (-3, 89), (-2, 165), (-1, 173),
        (0, 240), (1, 280), (2, 320), (3, 357), (4, 356), (5, 303), (6, 330), (7, 326), (8, 348), (9, 335),
        (10, 315), (11, 215), (12, 169), (13, 151), (14, 105), (15, 74),
    }.Select(x => new TemperatureBin(x.T, x.H)).ToList();

    private static readonly (double T, double Flow)[] LowTemperatureOutlet =
        { (-10, 35), (-7, 34), (2, 30), (7, 27), (12, 24), (15, 22.5) };

    /// <summary>Variable outlet temperature for the low temperature application (EN 14825, table 21).</summary>
    public static double LowTemperatureFlow(double outdoor)
    {
        if (outdoor <= LowTemperatureOutlet[0].T) return LowTemperatureOutlet[0].Flow;
        for (var i = 1; i < LowTemperatureOutlet.Length; i++)
        {
            var (t0, f0) = LowTemperatureOutlet[i - 1];
            var (t1, f1) = LowTemperatureOutlet[i];
            if (outdoor <= t1) return f0 + (f1 - f0) * (outdoor - t0) / (t1 - t0);
        }
        return LowTemperatureOutlet[^1].Flow;
    }

    /// <summary>
    /// Model SCOP for the average climate at 35 °C: part load P(T_j) = P_design·(T_j − 16)/(T_design − 16),
    /// P_design = rated heat output at 35 °C, deficit covered by an electric back-up heater.
    /// </summary>
    public static double ModelScop(HeatPumpModel hp)
    {
        var design = hp.RatedPowerLowTemp * 1000;
        double heat = 0, electricity = 0;
        foreach (var bin in AverageClimateBins)
        {
            var load = design * (bin.Temperature - 16) / (DesignTemperature - 16);
            var op = HeatPumpSimulator.OperatingPoint(hp, bin.Temperature, LowTemperatureFlow(bin.Temperature));
            var (hpHeat, hpElectric) = HeatPumpSimulator.Operate(op, load);
            heat += load * bin.Hours;
            electricity += (hpElectric + (load - hpHeat) / HeatPumpSimulator.BackupEfficiency) * bin.Hours;
        }
        return electricity > 0 ? heat / electricity : 0;
    }
}
