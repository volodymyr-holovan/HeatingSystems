using System.Text.Json;
using System.Text.Json.Serialization;
using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Projects;

/// <summary>Layer reference stored in a project file.</summary>
public sealed record LayerData(int MaterialId, double Thickness);

/// <summary>
/// Serializable description of a building project. Reference data is stored by id, so projects stay small and
/// always use the current reference values of the database.
/// </summary>
public sealed record ProjectData
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public int ClimateId { get; init; }
    public double IndoorTemperature { get; init; } = 20;
    public double HeatedFloorArea { get; init; }
    public double HeatedVolume { get; init; }
    public double ExternalWallAreaGross { get; init; }
    public List<LayerData> WallLayers { get; init; } = new();
    public double WindowArea { get; init; }
    public int? WindowTypeId { get; init; }
    public double DoorArea { get; init; }
    public double DoorUValue { get; init; } = 1.8;
    public double RoofArea { get; init; }
    public RoofType RoofType { get; init; } = RoofType.UnheatedAttic;
    public List<LayerData> RoofLayers { get; init; } = new();
    public double FloorArea { get; init; }
    public FloorType FloorType { get; init; } = FloorType.SlabOnGround;
    public List<LayerData> FloorLayers { get; init; } = new();
    public double FloorPerimeter { get; init; }
    public double ThermalBridgeSurcharge { get; init; } = 0.05;
    public double MinimumAirChangeRate { get; init; } = 0.5;
    public double AirTightnessN50 { get; init; } = 4.0;
    public double ShieldingCoefficient { get; init; } = 0.03;
    public bool MechanicalVentilation { get; init; }
    public double HeatRecoveryEfficiency { get; init; }
    public double InternalGains { get; init; } = 4.0;
    public ThermalMassClass ThermalMass { get; init; } = ThermalMassClass.Medium;
    public EmitterType Emitter { get; init; } = EmitterType.Radiators;
    public bool IncludeHotWater { get; init; } = true;
    public int Occupants { get; init; } = 3;
    public double HotWaterLitresPerPersonDay { get; init; } = 45;
    public double HotWaterTemperature { get; init; } = 55;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static ProjectData FromJson(string json)
    {
        var data = JsonSerializer.Deserialize<ProjectData>(json, Options) ?? throw new JsonException("Empty project.");
        if (data.Version > CurrentVersion)
            throw new NotSupportedException($"Project format version {data.Version} is newer than supported ({CurrentVersion}).");
        return data;
    }

    public static ProjectData FromBuilding(BuildingInput b) => new()
    {
        ClimateId = b.Climate.Id,
        IndoorTemperature = b.IndoorTemperature,
        HeatedFloorArea = b.HeatedFloorArea,
        HeatedVolume = b.HeatedVolume,
        ExternalWallAreaGross = b.ExternalWallAreaGross,
        WallLayers = ToData(b.Wall),
        WindowArea = b.WindowArea,
        WindowTypeId = b.Window?.Id,
        DoorArea = b.DoorArea,
        DoorUValue = b.DoorUValue,
        RoofArea = b.RoofArea,
        RoofType = b.RoofType,
        RoofLayers = ToData(b.Roof),
        FloorArea = b.FloorArea,
        FloorType = b.FloorType,
        FloorLayers = ToData(b.Floor),
        FloorPerimeter = b.FloorPerimeter,
        ThermalBridgeSurcharge = b.ThermalBridgeSurcharge,
        MinimumAirChangeRate = b.MinimumAirChangeRate,
        AirTightnessN50 = b.AirTightnessN50,
        ShieldingCoefficient = b.ShieldingCoefficient,
        MechanicalVentilation = b.MechanicalVentilation,
        HeatRecoveryEfficiency = b.HeatRecoveryEfficiency,
        InternalGains = b.InternalGains,
        ThermalMass = b.ThermalMass,
        Emitter = b.Emitter,
        IncludeHotWater = b.IncludeHotWater,
        Occupants = b.Occupants,
        HotWaterLitresPerPersonDay = b.HotWaterLitresPerPersonDay,
        HotWaterTemperature = b.HotWaterTemperature,
    };

    /// <summary>Resolves references and creates the calculation input.</summary>
    /// <exception cref="KeyNotFoundException">A referenced climate, window or material no longer exists.</exception>
    public BuildingInput ToBuilding(
        IReadOnlyDictionary<int, ClimateLocation> climates,
        IReadOnlyDictionary<int, Material> materials,
        IReadOnlyDictionary<int, WindowType> windows)
    {
        Construction Resolve(IEnumerable<LayerData> layers) =>
            new(layers.Select(l => new Layer(materials[l.MaterialId], l.Thickness)));

        return new BuildingInput
        {
            Climate = climates[ClimateId],
            IndoorTemperature = IndoorTemperature,
            HeatedFloorArea = HeatedFloorArea,
            HeatedVolume = HeatedVolume,
            ExternalWallAreaGross = ExternalWallAreaGross,
            Wall = Resolve(WallLayers),
            WindowArea = WindowArea,
            Window = WindowTypeId is { } w ? windows[w] : null,
            DoorArea = DoorArea,
            DoorUValue = DoorUValue,
            RoofArea = RoofArea,
            RoofType = RoofType,
            Roof = Resolve(RoofLayers),
            FloorArea = FloorArea,
            FloorType = FloorType,
            Floor = Resolve(FloorLayers),
            FloorPerimeter = FloorPerimeter,
            ThermalBridgeSurcharge = ThermalBridgeSurcharge,
            MinimumAirChangeRate = MinimumAirChangeRate,
            AirTightnessN50 = AirTightnessN50,
            ShieldingCoefficient = ShieldingCoefficient,
            MechanicalVentilation = MechanicalVentilation,
            HeatRecoveryEfficiency = HeatRecoveryEfficiency,
            InternalGains = InternalGains,
            ThermalMass = ThermalMass,
            Emitter = Emitter,
            IncludeHotWater = IncludeHotWater,
            Occupants = Occupants,
            HotWaterLitresPerPersonDay = HotWaterLitresPerPersonDay,
            HotWaterTemperature = HotWaterTemperature,
        };
    }

    private static List<LayerData> ToData(Construction c) => c.Layers.Select(l => new LayerData(l.Material.Id, l.Thickness)).ToList();
}
