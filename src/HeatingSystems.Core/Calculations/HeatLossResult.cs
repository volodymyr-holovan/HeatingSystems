using HeatingSystems.Core.Localization;

namespace HeatingSystems.Core.Calculations;

public enum LossCategory
{
    Transmission,
    Ground,
    Ventilation,
}

/// <summary>Heat loss of one envelope element or of ventilation.</summary>
/// <param name="NameKey">Localisation key of the element name.</param>
/// <param name="Area">Area, m² (0 for ventilation).</param>
/// <param name="UValue">Thermal transmittance used, W/(m²·K) (including thermal bridge surcharge).</param>
/// <param name="TemperatureFactor">Temperature correction factor b (or f_g1·f_g2 for ground).</param>
/// <param name="Coefficient">Heat loss coefficient H, W/K, at design conditions.</param>
/// <param name="DesignLoss">Design heat loss Φ, W.</param>
public sealed record HeatLossItem(
    string NameKey,
    LossCategory Category,
    double Area,
    double UValue,
    double TemperatureFactor,
    double Coefficient,
    double DesignLoss)
{
    public string Name => Localizer.T(NameKey);
}

/// <summary>Result of the design heat load calculation (EN 12831).</summary>
public sealed class HeatLossResult
{
    public required IReadOnlyList<HeatLossItem> Items { get; init; }

    /// <summary>Transmission heat loss coefficient to outdoor / unheated spaces H_T (without ground), W/K.</summary>
    public required double TransmissionCoefficient { get; init; }

    /// <summary>Ventilation heat loss coefficient H_V, W/K.</summary>
    public required double VentilationCoefficient { get; init; }

    /// <summary>Effective air flow V_eff used for the ventilation loss, m³/h.</summary>
    public required double EffectiveAirFlow { get; init; }

    /// <summary>Steady-state ground coefficient H_g = A·U, W/K.</summary>
    public required double GroundCoefficient { get; init; }

    /// <summary>Periodic external ground coefficient H_pe, W/K.</summary>
    public required double GroundPeriodicCoefficient { get; init; }

    public required double DesignTemperatureDifference { get; init; }

    public double TransmissionLoss => Items.Where(i => i.Category == LossCategory.Transmission).Sum(i => i.DesignLoss);
    public double GroundLoss => Items.Where(i => i.Category == LossCategory.Ground).Sum(i => i.DesignLoss);
    public double VentilationLoss => Items.Where(i => i.Category == LossCategory.Ventilation).Sum(i => i.DesignLoss);

    /// <summary>Design heat load Φ_HL, W.</summary>
    public double DesignHeatLoad => Items.Sum(i => i.DesignLoss);

    /// <summary>Specific design heat load, W/m².</summary>
    public required double SpecificHeatLoad { get; init; }
}
