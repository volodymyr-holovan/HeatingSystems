using HeatingSystems.Core.Models;

namespace HeatingSystems.Tests;

internal static class TestData
{
    public static readonly Material Brick = new(1, "Цегла", MaterialCategory.Masonry, 0.81, 1800, "test");
    public static readonly Material Eps = new(14, "EPS", MaterialCategory.Insulation, 0.040, 20, "test");
    public static readonly Material Plaster = new(23, "Штукатурка", MaterialCategory.Finish, 0.93, 1800, "test");
    public static readonly Material MineralWool = new(18, "Мінвата", MaterialCategory.Insulation, 0.037, 15, "test");
    public static readonly Material Concrete = new(9, "Залізобетон", MaterialCategory.Concrete, 2.04, 2500, "test");
    public static readonly WindowType Pvc = new(5, "ПВХ", 1.3, 0.6, "test");

    public static readonly ClimateLocation Kyiv = new(1, "Київ", "м. Київ", -22, 176, -0.1, 7.7, "test");

    public static BuildingInput House(ClimateLocation? climate = null, EmitterType emitter = EmitterType.Radiators,
        double internalGains = 4.0, bool hotWater = true) => new()
    {
        Climate = climate ?? Kyiv,
        IndoorTemperature = 20,
        HeatedFloorArea = 100,
        HeatedVolume = 250,
        ExternalWallAreaGross = 120,
        Wall = new Construction(new[] { new Layer(Brick, 0.38), new Layer(Eps, 0.10), new Layer(Plaster, 0.02) }),
        WindowArea = 15,
        Window = Pvc,
        DoorArea = 2,
        DoorUValue = 1.8,
        RoofArea = 100,
        RoofType = RoofType.UnheatedAttic,
        Roof = new Construction(new[] { new Layer(Concrete, 0.2), new Layer(MineralWool, 0.2) }),
        FloorArea = 100,
        FloorType = FloorType.SlabOnGround,
        Floor = new Construction(new[] { new Layer(Eps, 0.1) }),
        FloorPerimeter = 40,
        ThermalBridgeSurcharge = 0.05,
        MinimumAirChangeRate = 0.5,
        AirTightnessN50 = 4,
        ShieldingCoefficient = 0.03,
        InternalGains = internalGains,
        Emitter = emitter,
        IncludeHotWater = hotWater,
    };

    /// <summary>
    /// Air/water inverter heat pump with hplib coefficients of a real KEYMARK product (Acond Aconomis N, hplib database row 1).
    /// </summary>
    public static HeatPumpModel AirHeatPump(double scale = 1.0) => new()
    {
        Id = 1,
        Manufacturer = "Acond a.s.",
        Model = "Acond Aconomis N",
        Series = "Acond Aconomis N",
        Source = HeatSource.Air,
        Control = CompressorControl.Regulated,
        Refrigerant = "R290",
        RatedPowerLowTemp = 7.65,
        RatedPowerMediumTemp = 7.5,
        Scop = 4.75,
        MaxFlowTemperature = 75,
        OperationLimitTemperature = -10,
        ThermalPowerRef = 6580 * scale,
        ElectricPowerRef = 2873 * scale,
        CopP1 = 0.7275923986463755,
        CopP2 = -0.0727164455094132,
        CopP3 = 6.752011728451605,
        CopP4 = -0.5843449502219047,
        PelP1 = -43.12772747054501,
        PelP2 = 0.0102814079314003,
        PelP3 = 0.0712570775813438,
        PelP4 = 43.07436724078105,
    };
}
