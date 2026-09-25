namespace HeatingSystems.Core.Models;

public enum HeatSource
{
    /// <summary>Outdoor air / water.</summary>
    Air = 1,
    /// <summary>Brine (ground collector) / water.</summary>
    Brine = 2,
    /// <summary>Water (ground water) / water.</summary>
    Water = 3,
}

public enum CompressorControl
{
    /// <summary>Inverter-driven, modulating compressor.</summary>
    Regulated = 1,
    OnOff = 2,
}

/// <summary>
/// Heat pump from the Heat Pump KEYMARK certified product population (hplib database).
/// Performance maps follow the hplib regression model:
/// COP = p1·T_in + p2·T_out + p3 + p4·T_amb,
/// P_el = P_el,ref · (k1·T_in + k2·T_out + k3 + k4·T_amb).
/// </summary>
public sealed class HeatPumpModel
{
    public int Id { get; init; }
    public string Manufacturer { get; init; } = "";
    public string Series { get; init; } = "";
    public string Model { get; init; } = "";
    public HeatSource Source { get; init; }
    public CompressorControl Control { get; init; }
    public DateOnly? CertificationDate { get; init; }
    public string Refrigerant { get; init; } = "";
    public double? RefrigerantMassKg { get; init; }

    /// <summary>Rated heat output at 35 °C (low temperature application), kW.</summary>
    public double RatedPowerLowTemp { get; init; }
    /// <summary>Rated heat output at 55 °C (medium temperature application), kW.</summary>
    public double RatedPowerMediumTemp { get; init; }
    /// <summary>SCOP according to EN 14825, average climate, low temperature application.</summary>
    public double Scop { get; init; }
    /// <summary>Seasonal space heating energy efficiency η_s (ErP), low temperature, %.</summary>
    public double EtaSLowTemp { get; init; }
    /// <summary>Seasonal space heating energy efficiency η_s (ErP), medium temperature, %.</summary>
    public double EtaSMediumTemp { get; init; }
    public double? SoundPowerOutdoor { get; init; }
    public double? SoundPowerIndoor { get; init; }
    public double MaxFlowTemperature { get; init; }
    public double? BivalentTemperature { get; init; }
    public double? OperationLimitTemperature { get; init; }

    /// <summary>Thermal output at reference point (A-7/W52 for air, B0/W52 brine, W10/W52 water), W.</summary>
    public double ThermalPowerRef { get; init; }
    /// <summary>Electrical input at reference point, W.</summary>
    public double ElectricPowerRef { get; init; }

    public double CopP1 { get; init; }
    public double CopP2 { get; init; }
    public double CopP3 { get; init; }
    public double CopP4 { get; init; }
    public double PelP1 { get; init; }
    public double PelP2 { get; init; }
    public double PelP3 { get; init; }
    public double PelP4 { get; init; }

    public string DisplayName => $"{Manufacturer} — {Model}";

    public override string ToString() => DisplayName;
}
