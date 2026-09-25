using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Models;
using HeatingSystems.Core.Projects;

namespace HeatingSystems.App.ViewModels;

/// <summary>Input form of the building (page "Building").</summary>
public sealed partial class BuildingViewModel : PageViewModel
{
    private readonly ReferenceCache _reference;

    public BuildingViewModel(ReferenceCache reference)
        : base("page.building", "\uE80F", "page.building.subtitle")
    {
        _reference = reference;
        BuildLists();
        ResetToDefaults();
    }

    // New list instances make combo boxes regenerate their items, so names follow the UI language.
    public IReadOnlyList<ClimateLocation> Climates { get; private set; } = Array.Empty<ClimateLocation>();
    public IReadOnlyList<Material> StructuralMaterials { get; private set; } = Array.Empty<Material>();
    public IReadOnlyList<Material> InsulationMaterials { get; private set; } = Array.Empty<Material>();
    public IReadOnlyList<WindowType> WindowTypes { get; private set; } = Array.Empty<WindowType>();

    private void BuildLists()
    {
        Climates = _reference.Climates.OrderBy(c => c.City, StringComparer.CurrentCulture).ToList();
        StructuralMaterials = _reference.Materials.Where(m => m.Category != MaterialCategory.Insulation).ToList();
        InsulationMaterials = _reference.Materials.Where(m => m.Category == MaterialCategory.Insulation).ToList();
        WindowTypes = _reference.Windows.ToList();
    }

    public override void RefreshLanguage()
    {
        // Keep the selections: the lists contain the same objects.
        var (climate, wall, wallIns, roof, roofIns, floor, floorIns, window) =
            (SelectedClimate, WallMaterial, WallInsulation, RoofMaterial, RoofInsulation, FloorMaterial, FloorInsulation, WindowType);
        BuildLists();
        base.RefreshLanguage();
        (SelectedClimate, WallMaterial, WallInsulation, RoofMaterial, RoofInsulation, FloorMaterial, FloorInsulation, WindowType) =
            (climate, wall, wallIns, roof, roofIns, floor, floorIns, window);
    }
    public IReadOnlyList<Option<RoofType>> RoofTypes => DisplayNames.RoofTypes;
    public IReadOnlyList<Option<FloorType>> FloorTypes => DisplayNames.FloorTypes;
    public IReadOnlyList<Option<EmitterType>> Emitters => DisplayNames.Emitters;
    public IReadOnlyList<Option<ThermalMassClass>> ThermalMasses => DisplayNames.ThermalMasses;
    public IReadOnlyList<Option<double>> ShieldingOptions => DisplayNames.Shielding;

    // ---- climate ----
    [ObservableProperty] private ClimateLocation? _selectedClimate;
    [ObservableProperty] private double _indoorTemperature;

    // ---- geometry ----
    [ObservableProperty] private double _heatedFloorArea;
    [ObservableProperty] private double _ceilingHeight;

    // ---- walls ----
    [ObservableProperty] private double _externalWallArea;
    [ObservableProperty] private Material? _wallMaterial;
    [ObservableProperty] private double _wallThicknessMm;
    [ObservableProperty] private Material? _wallInsulation;
    [ObservableProperty] private double _wallInsulationMm;

    // ---- windows / doors ----
    [ObservableProperty] private WindowType? _windowType;
    [ObservableProperty] private double _windowArea;
    [ObservableProperty] private double _doorArea;
    [ObservableProperty] private double _doorUValue;

    // ---- roof ----
    [ObservableProperty] private Option<RoofType>? _roofType;
    [ObservableProperty] private double _roofArea;
    [ObservableProperty] private Material? _roofMaterial;
    [ObservableProperty] private double _roofThicknessMm;
    [ObservableProperty] private Material? _roofInsulation;
    [ObservableProperty] private double _roofInsulationMm;

    // ---- floor ----
    [ObservableProperty] private Option<FloorType>? _floorType;
    [ObservableProperty] private double _floorArea;
    [ObservableProperty] private double _floorPerimeter;
    [ObservableProperty] private Material? _floorMaterial;
    [ObservableProperty] private double _floorThicknessMm;
    [ObservableProperty] private Material? _floorInsulation;
    [ObservableProperty] private double _floorInsulationMm;

    // ---- ventilation and gains ----
    [ObservableProperty] private double _thermalBridgeSurcharge;
    [ObservableProperty] private bool _mechanicalVentilation;
    [ObservableProperty] private double _heatRecoveryPercent;
    [ObservableProperty] private double _airChangeRate;
    [ObservableProperty] private double _airTightnessN50;
    [ObservableProperty] private Option<double>? _shielding;
    [ObservableProperty] private double _internalGains;
    [ObservableProperty] private Option<ThermalMassClass>? _thermalMass;

    // ---- heating system and DHW ----
    [ObservableProperty] private Option<EmitterType>? _emitter;
    [ObservableProperty] private bool _includeHotWater;
    [ObservableProperty] private int _occupants;
    [ObservableProperty] private double _hotWaterLitres;
    [ObservableProperty] private double _hotWaterTemperature;

    public double HeatedVolume => HeatedFloorArea * CeilingHeight;

    public bool IsSlabOnGround => FloorType?.Value == Core.Models.FloorType.SlabOnGround;

    public Construction WallConstruction => Build(WallMaterial, WallThicknessMm, WallInsulation, WallInsulationMm);
    public Construction RoofConstruction => Build(RoofMaterial, RoofThicknessMm, RoofInsulation, RoofInsulationMm);
    public Construction FloorConstruction => Build(FloorMaterial, FloorThicknessMm, FloorInsulation, FloorInsulationMm);

    /// <summary>Live U-value of the wall, W/(m²·K).</summary>
    public double WallUValue => ThermalTransmittance.UValue(WallConstruction, HeatFlowDirection.Horizontal);

    public double RoofUValue => ThermalTransmittance.UValue(RoofConstruction, HeatFlowDirection.Upwards,
        RoofType?.Value == Core.Models.RoofType.UnheatedAttic);

    /// <summary>U-value of the floor: EN ISO 13370 for slabs on ground, EN ISO 6946 otherwise.</summary>
    public double FloorUValue => IsSlabOnGround
        ? GroundHeatTransfer.SlabUValue(FloorArea, FloorPerimeter, Math.Max(WallConstruction.Thickness, 0.3), FloorConstruction)
        : ThermalTransmittance.UValue(FloorConstruction, HeatFlowDirection.Downwards,
            FloorType?.Value == Core.Models.FloorType.AboveUnheatedBasement);

    public double DesignTemperature => SelectedClimate?.DesignTemperature ?? 0;

    public string ClimateSummary => SelectedClimate is { } c
        ? Localizer.F("building.climateSummary", c.DesignTemperature, c.HeatingSeasonDays, c.HeatingSeasonMeanTemperature,
            c.DegreeDays(IndoorTemperature), c.ClimateZone)
        : Localizer.T("validation.city");

    /// <summary>Validation messages of the current input (empty when valid).</summary>
    public IReadOnlyList<string> ValidationErrors
    {
        get
        {
            if (SelectedClimate is null) return new[] { Localizer.T("validation.city") };
            var errors = new List<string>();
            if (WallMaterial is null || RoofMaterial is null || FloorMaterial is null) errors.Add(Localizer.T("validation.materials"));
            if (errors.Count == 0) errors.AddRange(ToBuildingInput().Validate());
            return errors;
        }
    }

    public bool HasErrors => ValidationErrors.Count > 0;

    private static readonly HashSet<string> DerivedProperties = new()
    {
        nameof(HeatedVolume), nameof(IsSlabOnGround), nameof(WallConstruction), nameof(RoofConstruction),
        nameof(FloorConstruction), nameof(WallUValue), nameof(RoofUValue), nameof(FloorUValue), nameof(DesignTemperature),
        nameof(ClimateSummary), nameof(ValidationErrors), nameof(HasErrors),
    };

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is null || DerivedProperties.Contains(e.PropertyName)) return;
        foreach (var name in DerivedProperties) base.OnPropertyChanged(new PropertyChangedEventArgs(name));
    }

    private static Construction Build(Material? baseMaterial, double baseMm, Material? insulation, double insulationMm)
    {
        var layers = new List<Layer>();
        if (baseMaterial is not null && baseMm > 0) layers.Add(new Layer(baseMaterial, baseMm / 1000.0));
        if (insulation is not null && insulationMm > 0) layers.Add(new Layer(insulation, insulationMm / 1000.0));
        return new Construction(layers);
    }

    public BuildingInput ToBuildingInput()
    {
        var climate = SelectedClimate ?? throw new InvalidOperationException(Localizer.T("validation.city"));
        return new BuildingInput
        {
            Climate = climate,
            IndoorTemperature = IndoorTemperature,
            HeatedFloorArea = HeatedFloorArea,
            HeatedVolume = HeatedVolume,
            ExternalWallAreaGross = ExternalWallArea,
            Wall = WallConstruction,
            WindowArea = WindowArea,
            Window = WindowType,
            DoorArea = DoorArea,
            DoorUValue = DoorUValue,
            RoofArea = RoofArea,
            RoofType = RoofType?.Value ?? Core.Models.RoofType.UnheatedAttic,
            Roof = RoofConstruction,
            FloorArea = FloorArea,
            FloorType = FloorType?.Value ?? Core.Models.FloorType.SlabOnGround,
            Floor = FloorConstruction,
            FloorPerimeter = FloorPerimeter,
            ThermalBridgeSurcharge = ThermalBridgeSurcharge,
            MinimumAirChangeRate = AirChangeRate,
            AirTightnessN50 = AirTightnessN50,
            ShieldingCoefficient = Shielding?.Value ?? 0.03,
            MechanicalVentilation = MechanicalVentilation,
            HeatRecoveryEfficiency = MechanicalVentilation ? HeatRecoveryPercent / 100.0 : 0,
            InternalGains = InternalGains,
            ThermalMass = ThermalMass?.Value ?? ThermalMassClass.Medium,
            Emitter = Emitter?.Value ?? EmitterType.Radiators,
            IncludeHotWater = IncludeHotWater,
            Occupants = Occupants,
            HotWaterLitresPerPersonDay = HotWaterLitres,
            HotWaterTemperature = HotWaterTemperature,
        };
    }

    // Reference data ids of the default house (database/seed_reference.sql).
    private const int KyivId = 1, AeratedConcreteD400 = 5, StoneWool = 17, ReinforcedConcrete = 9, GlassWool = 18,
        GravelConcrete = 10, ExtrudedPolystyrene = 16, PvcLowEWindow = 5;

    /// <summary>Typical detached house (120 m², aerated concrete with mineral wool, Kyiv).</summary>
    public void ResetToDefaults()
    {
        Material? Find(int id) => _reference.MaterialById.GetValueOrDefault(id);

        SelectedClimate = _reference.ClimateById.GetValueOrDefault(KyivId) ?? _reference.Climates.FirstOrDefault();
        IndoorTemperature = 20;
        HeatedFloorArea = 120;
        CeilingHeight = 2.7;
        ExternalWallArea = 150;
        WallMaterial = Find(AeratedConcreteD400) ?? StructuralMaterials.FirstOrDefault();
        WallThicknessMm = 375;
        WallInsulation = Find(StoneWool) ?? InsulationMaterials.FirstOrDefault();
        WallInsulationMm = 100;
        WindowType = _reference.WindowById.GetValueOrDefault(PvcLowEWindow) ?? _reference.Windows.FirstOrDefault();
        WindowArea = 22;
        DoorArea = 2.2;
        DoorUValue = 1.8;
        RoofType = DisplayNames.RoofTypes[0];
        RoofArea = 75;
        RoofMaterial = Find(ReinforcedConcrete) ?? StructuralMaterials.FirstOrDefault();
        RoofThicknessMm = 200;
        RoofInsulation = Find(GlassWool) ?? InsulationMaterials.FirstOrDefault();
        RoofInsulationMm = 250;
        FloorType = DisplayNames.FloorTypes[0];
        FloorArea = 75;
        FloorPerimeter = 35;
        FloorMaterial = Find(GravelConcrete) ?? StructuralMaterials.FirstOrDefault();
        FloorThicknessMm = 100;
        FloorInsulation = Find(ExtrudedPolystyrene) ?? InsulationMaterials.FirstOrDefault();
        FloorInsulationMm = 100;
        ThermalBridgeSurcharge = 0.05;
        MechanicalVentilation = false;
        HeatRecoveryPercent = 80;
        AirChangeRate = 0.5;
        AirTightnessN50 = 3.0;
        Shielding = DisplayNames.Shielding[1];
        InternalGains = 4;
        ThermalMass = DisplayNames.ThermalMasses[1];
        Emitter = DisplayNames.Emitters[2];
        IncludeHotWater = true;
        Occupants = 4;
        HotWaterLitres = 45;
        HotWaterTemperature = 55;
    }

    public ProjectData ToProjectData() => ProjectData.FromBuilding(ToBuildingInput());

    /// <summary>Fills the form from a saved project (two-layer constructions: base layer + insulation).</summary>
    public void Load(ProjectData data)
    {
        var b = data.ToBuilding(_reference.ClimateById, _reference.MaterialById, _reference.WindowById);
        SelectedClimate = b.Climate;
        IndoorTemperature = b.IndoorTemperature;
        HeatedFloorArea = b.HeatedFloorArea;
        CeilingHeight = b.HeatedFloorArea > 0 ? Math.Round(b.HeatedVolume / b.HeatedFloorArea, 3) : 2.7;
        ExternalWallArea = b.ExternalWallAreaGross;
        (WallMaterial, WallThicknessMm, WallInsulation, WallInsulationMm) = Split(b.Wall, WallMaterial, WallInsulation);
        WindowType = b.Window ?? WindowType;
        WindowArea = b.WindowArea;
        DoorArea = b.DoorArea;
        DoorUValue = b.DoorUValue;
        RoofType = DisplayNames.RoofTypes.First(o => o.Value == b.RoofType);
        RoofArea = b.RoofArea;
        (RoofMaterial, RoofThicknessMm, RoofInsulation, RoofInsulationMm) = Split(b.Roof, RoofMaterial, RoofInsulation);
        FloorType = DisplayNames.FloorTypes.First(o => o.Value == b.FloorType);
        FloorArea = b.FloorArea;
        FloorPerimeter = b.FloorPerimeter;
        (FloorMaterial, FloorThicknessMm, FloorInsulation, FloorInsulationMm) = Split(b.Floor, FloorMaterial, FloorInsulation);
        ThermalBridgeSurcharge = b.ThermalBridgeSurcharge;
        MechanicalVentilation = b.MechanicalVentilation;
        HeatRecoveryPercent = b.MechanicalVentilation ? b.HeatRecoveryEfficiency * 100 : 80;
        AirChangeRate = b.MinimumAirChangeRate;
        AirTightnessN50 = b.AirTightnessN50;
        Shielding = DisplayNames.Shielding.OrderBy(o => Math.Abs(o.Value - b.ShieldingCoefficient)).First();
        InternalGains = b.InternalGains;
        ThermalMass = DisplayNames.ThermalMasses.First(o => o.Value == b.ThermalMass);
        Emitter = DisplayNames.Emitters.First(o => o.Value == b.Emitter);
        IncludeHotWater = b.IncludeHotWater;
        Occupants = b.Occupants;
        HotWaterLitres = b.HotWaterLitresPerPersonDay;
        HotWaterTemperature = b.HotWaterTemperature;
    }

    private static (Material?, double, Material?, double) Split(Construction c, Material? baseFallback, Material? insulationFallback)
    {
        var baseLayer = c.Layers.FirstOrDefault(l => l.Material.Category != MaterialCategory.Insulation);
        var insulation = c.Layers.FirstOrDefault(l => l.Material.Category == MaterialCategory.Insulation);
        return (baseLayer?.Material ?? baseFallback, (baseLayer?.Thickness ?? 0) * 1000,
                insulation?.Material ?? insulationFallback, (insulation?.Thickness ?? 0) * 1000);
    }
}
