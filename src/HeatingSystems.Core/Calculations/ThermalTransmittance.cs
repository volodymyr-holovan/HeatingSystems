using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>Direction of heat flow through a building element (EN ISO 6946, table 7).</summary>
public enum HeatFlowDirection
{
    Horizontal,
    Upwards,
    Downwards,
}

/// <summary>Thermal transmittance of building components according to EN ISO 6946.</summary>
public static class ThermalTransmittance
{
    /// <summary>Internal surface resistance R_si, m²·K/W.</summary>
    public static double InternalSurfaceResistance(HeatFlowDirection direction) => direction switch
    {
        HeatFlowDirection.Upwards => 0.10,
        HeatFlowDirection.Horizontal => 0.13,
        HeatFlowDirection.Downwards => 0.17,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>External surface resistance R_se towards outdoor air, m²·K/W.</summary>
    public const double ExternalSurfaceResistance = 0.04;

    /// <summary>
    /// U = 1 / (R_si + Σ d/λ + R_se). When the element adjoins an unheated space,
    /// R_se is taken equal to R_si (EN ISO 6946, 6.8).
    /// </summary>
    public static double UValue(Construction construction, HeatFlowDirection direction, bool adjoinsUnheatedSpace = false)
    {
        var rsi = InternalSurfaceResistance(direction);
        var rse = adjoinsUnheatedSpace ? rsi : ExternalSurfaceResistance;
        return 1.0 / (rsi + construction.LayersResistance + rse);
    }

    /// <summary>Total thermal resistance R_T including surface resistances, m²·K/W.</summary>
    public static double TotalResistance(Construction construction, HeatFlowDirection direction, bool adjoinsUnheatedSpace = false) =>
        1.0 / UValue(construction, direction, adjoinsUnheatedSpace);
}
