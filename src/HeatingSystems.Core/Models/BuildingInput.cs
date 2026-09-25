namespace HeatingSystems.Core.Models;

/// <summary>A homogeneous layer of a construction.</summary>
/// <param name="Thickness">Layer thickness d, m.</param>
public sealed record Layer(Material Material, double Thickness)
{
    /// <summary>Thermal resistance R = d / λ, m²·K/W.</summary>
    public double ThermalResistance => Material.Conductivity > 0 ? Thickness / Material.Conductivity : 0;
}

/// <summary>Opaque multi-layer construction (wall, roof, floor).</summary>
public sealed class Construction
{
    public Construction(IEnumerable<Layer> layers) => Layers = layers.Where(l => l.Thickness > 0).ToList();

    public IReadOnlyList<Layer> Layers { get; }

    public double Thickness => Layers.Sum(l => l.Thickness);

    /// <summary>Sum of the layers' thermal resistances (without surface resistances), m²·K/W.</summary>
    public double LayersResistance => Layers.Sum(l => l.ThermalResistance);

    public static Construction Empty { get; } = new(Array.Empty<Layer>());
}

public enum RoofType
{
    /// <summary>Roof / ceiling directly exposed to outdoor air (b = 1).</summary>
    ExposedRoof = 1,
    /// <summary>Top floor ceiling under an unheated ventilated attic.</summary>
    UnheatedAttic = 2,
}

public enum FloorType
{
    /// <summary>Slab on ground, EN ISO 13370.</summary>
    SlabOnGround = 1,
    /// <summary>Floor above an unheated basement / crawl space.</summary>
    AboveUnheatedBasement = 2,
    /// <summary>Floor above outdoor air (e.g. passage).</summary>
    AboveOutdoorAir = 3,
}

public enum ThermalMassClass
{
    Light = 1,
    Medium = 2,
    Heavy = 3,
}

public enum EmitterType
{
    /// <summary>Underfloor heating, design flow 35 °C.</summary>
    Underfloor = 1,
    /// <summary>Low-temperature (oversized) radiators / fan coils, design flow 45 °C.</summary>
    LowTemperatureRadiators = 2,
    /// <summary>Standard panel radiators, design flow 55 °C.</summary>
    Radiators = 3,
    /// <summary>Legacy high-temperature radiators, design flow 70 °C.</summary>
    HighTemperatureRadiators = 4,
}

/// <summary>Complete description of a building for heat load and annual energy calculation.</summary>
public sealed class BuildingInput
{
    public required ClimateLocation Climate { get; init; }

    /// <summary>Design indoor temperature θ_int, °C.</summary>
    public double IndoorTemperature { get; init; } = 20.0;

    /// <summary>Heated floor area A_f, m².</summary>
    public double HeatedFloorArea { get; init; }

    /// <summary>Heated internal air volume V, m³.</summary>
    public double HeatedVolume { get; init; }

    // ---- Walls ----
    /// <summary>Gross external wall area including windows and doors, m².</summary>
    public double ExternalWallAreaGross { get; init; }
    public Construction Wall { get; init; } = Construction.Empty;

    // ---- Windows / doors ----
    public double WindowArea { get; init; }
    public WindowType? Window { get; init; }
    public double DoorArea { get; init; }
    /// <summary>External door thermal transmittance, W/(m²·K).</summary>
    public double DoorUValue { get; init; } = 1.8;

    // ---- Roof ----
    public double RoofArea { get; init; }
    public RoofType RoofType { get; init; } = RoofType.UnheatedAttic;
    public Construction Roof { get; init; } = Construction.Empty;

    // ---- Floor ----
    public double FloorArea { get; init; }
    public FloorType FloorType { get; init; } = FloorType.SlabOnGround;
    public Construction Floor { get; init; } = Construction.Empty;
    /// <summary>Exposed perimeter of the ground floor slab P, m (EN ISO 13370).</summary>
    public double FloorPerimeter { get; init; }

    /// <summary>Thermal bridge surcharge ΔU_TB, W/(m²·K) (EN 12831 simplified method).</summary>
    public double ThermalBridgeSurcharge { get; init; } = 0.05;

    // ---- Ventilation ----
    /// <summary>Minimum hygienic air change rate n_min, 1/h.</summary>
    public double MinimumAirChangeRate { get; init; } = 0.5;
    /// <summary>Air change rate at 50 Pa pressure difference n50, 1/h (blower-door test).</summary>
    public double AirTightnessN50 { get; init; } = 4.0;
    /// <summary>Shielding coefficient e (EN 12831 table D.8), –.</summary>
    public double ShieldingCoefficient { get; init; } = 0.03;
    public bool MechanicalVentilation { get; init; }
    /// <summary>Heat recovery efficiency of mechanical ventilation η_v, –.</summary>
    public double HeatRecoveryEfficiency { get; init; }

    // ---- Gains and dynamics ----
    /// <summary>Average internal heat gains, W/m² of heated floor area.</summary>
    public double InternalGains { get; init; } = 4.0;
    public ThermalMassClass ThermalMass { get; init; } = ThermalMassClass.Medium;

    // ---- Heating system ----
    public EmitterType Emitter { get; init; } = EmitterType.Radiators;

    // ---- Domestic hot water ----
    public int Occupants { get; init; } = 3;
    /// <summary>Daily hot water use per person at <see cref="HotWaterTemperature"/>, litres.</summary>
    public double HotWaterLitresPerPersonDay { get; init; } = 45;
    public double HotWaterTemperature { get; init; } = 55;
    public double ColdWaterTemperature { get; init; } = 10;
    /// <summary>Storage and distribution losses as a share of the net DHW demand, –.</summary>
    public double HotWaterLossFactor { get; init; } = 0.15;
    public bool IncludeHotWater { get; init; } = true;

    /// <summary>Net opaque wall area (gross area minus windows and doors), m².</summary>
    public double NetWallArea => Math.Max(0, ExternalWallAreaGross - WindowArea - DoorArea);

    public IEnumerable<string> Validate()
    {
        if (HeatedFloorArea <= 0) yield return "Опалювана площа має бути більшою за нуль.";
        if (HeatedVolume <= 0) yield return "Опалюваний об'єм має бути більшим за нуль.";
        if (HeatedVolume > 0 && HeatedFloorArea > 0 && HeatedVolume / HeatedFloorArea is < 2.0 or > 10.0)
            yield return "Середня висота приміщень (об'єм / площа) виходить за межі 2–10 м.";
        if (ExternalWallAreaGross < 0 || WindowArea < 0 || DoorArea < 0 || RoofArea < 0 || FloorArea < 0)
            yield return "Площі не можуть бути від'ємними.";
        if (WindowArea + DoorArea > ExternalWallAreaGross)
            yield return "Площа вікон і дверей перевищує загальну площу зовнішніх стін.";
        if (WindowArea > 0 && Window is null) yield return "Не вибрано тип вікон.";
        if (IndoorTemperature <= Climate.DesignTemperature)
            yield return "Внутрішня температура має бути вищою за розрахункову зовнішню.";
        if (IndoorTemperature is < 10 or > 30) yield return "Внутрішня температура має бути в межах 10–30 °C.";
        if (FloorType == FloorType.SlabOnGround && FloorArea > 0 && FloorPerimeter <= 0)
            yield return "Для підлоги по ґрунту потрібно вказати периметр.";
        if (HeatRecoveryEfficiency is < 0 or > 0.95) yield return "ККД рекуперації має бути в межах 0–0,95.";
        if (AirTightnessN50 < 0 || MinimumAirChangeRate < 0) yield return "Кратність повітрообміну не може бути від'ємною.";
        if (IncludeHotWater && HotWaterTemperature <= ColdWaterTemperature)
            yield return "Температура гарячої води має бути вищою за температуру холодної.";
    }

    /// <summary>Design flow temperature of the heat emitters, °C.</summary>
    public double DesignFlowTemperature => Emitter switch
    {
        EmitterType.Underfloor => 35,
        EmitterType.LowTemperatureRadiators => 45,
        EmitterType.Radiators => 55,
        EmitterType.HighTemperatureRadiators => 70,
        _ => 55,
    };

    /// <summary>Emitter exponent n of the characteristic Φ ~ ΔT^n (EN 442 / EN 1264).</summary>
    public double EmitterExponent => Emitter == EmitterType.Underfloor ? 1.1 : 1.3;
}
