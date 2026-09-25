using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>Heat transfer via the ground for slab-on-ground floors according to EN ISO 13370.</summary>
public static class GroundHeatTransfer
{
    /// <summary>Thermal conductivity of unfrozen ground (clay or silt), W/(m·K) — EN ISO 13370, table 7.</summary>
    public const double GroundConductivity = 2.0;

    /// <summary>Periodic penetration depth δ for clay/silt, m — EN ISO 13370, table 7.</summary>
    public const double PeriodicPenetrationDepth = 3.2;

    private const double Rsi = 0.17;
    private const double Rse = 0.04;

    /// <summary>Characteristic dimension B' = A / (0.5·P), m.</summary>
    public static double CharacteristicDimension(double area, double perimeter) =>
        perimeter > 0 ? area / (0.5 * perimeter) : double.PositiveInfinity;

    /// <summary>Equivalent thickness d_t = w + λ_g·(R_si + R_f + R_se), m.</summary>
    public static double EquivalentThickness(double wallThickness, double floorResistance) =>
        wallThickness + GroundConductivity * (Rsi + floorResistance + Rse);

    /// <summary>
    /// Steady-state thermal transmittance of a slab-on-ground floor U, W/(m²·K):
    /// d_t &lt; B' (uninsulated / moderately insulated): U = 2λ/(πB' + d_t) · ln(πB'/d_t + 1);
    /// d_t ≥ B' (well insulated): U = λ / (0.457·B' + d_t).
    /// </summary>
    public static double SlabUValue(double area, double perimeter, double wallThickness, Construction floor)
    {
        if (area <= 0) return 0;
        var b = CharacteristicDimension(area, perimeter);
        var dt = EquivalentThickness(wallThickness, floor.LayersResistance);
        const double lambda = GroundConductivity;
        return dt < b
            ? 2 * lambda / (Math.PI * b + dt) * Math.Log(Math.PI * b / dt + 1)
            : lambda / (0.457 * b + dt);
    }

    /// <summary>
    /// External periodic heat transfer coefficient H_pe = 0.37·P·λ·ln(δ/d_t + 1), W/K
    /// (EN ISO 13370, annex A, slab without edge insulation).
    /// </summary>
    public static double PeriodicCoefficient(double perimeter, double wallThickness, Construction floor)
    {
        if (perimeter <= 0) return 0;
        var dt = EquivalentThickness(wallThickness, floor.LayersResistance);
        return 0.37 * perimeter * GroundConductivity * Math.Log(PeriodicPenetrationDepth / dt + 1);
    }
}
