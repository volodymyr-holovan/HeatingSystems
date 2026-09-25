using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Models;

namespace HeatingSystems.App.ViewModels;

/// <summary>Localised names for enumerations.</summary>
public static class DisplayNames
{
    public static IReadOnlyList<Option<RoofType>> RoofTypes { get; } = new Option<RoofType>[]
    {
        new(RoofType.UnheatedAttic, "roofType.attic"),
        new(RoofType.ExposedRoof, "roofType.exposed"),
    };

    public static IReadOnlyList<Option<FloorType>> FloorTypes { get; } = new Option<FloorType>[]
    {
        new(FloorType.SlabOnGround, "floorType.slab"),
        new(FloorType.AboveUnheatedBasement, "floorType.basement"),
        new(FloorType.AboveOutdoorAir, "floorType.outdoor"),
    };

    public static IReadOnlyList<Option<EmitterType>> Emitters { get; } = new Option<EmitterType>[]
    {
        new(EmitterType.Underfloor, "emitter.underfloor"),
        new(EmitterType.LowTemperatureRadiators, "emitter.lowTemperature"),
        new(EmitterType.Radiators, "emitter.radiators"),
        new(EmitterType.HighTemperatureRadiators, "emitter.highTemperature"),
    };

    public static IReadOnlyList<Option<ThermalMassClass>> ThermalMasses { get; } = new Option<ThermalMassClass>[]
    {
        new(ThermalMassClass.Light, "mass.light"),
        new(ThermalMassClass.Medium, "mass.medium"),
        new(ThermalMassClass.Heavy, "mass.heavy"),
    };

    public static IReadOnlyList<Option<double>> Shielding { get; } = new Option<double>[]
    {
        new(0.05, "shielding.open"),
        new(0.03, "shielding.moderate"),
        new(0.02, "shielding.heavy"),
    };

    public static IReadOnlyList<Option<HeatSource?>> Sources { get; } = new Option<HeatSource?>[]
    {
        new(null, "catalog.allSources"),
        new(HeatSource.Air, "source.air"),
        new(HeatSource.Brine, "source.brine"),
        new(HeatSource.Water, "source.water"),
    };

    public static IReadOnlyList<Option<CompressorControl?>> Controls { get; } = new Option<CompressorControl?>[]
    {
        new(null, "catalog.anyControl"),
        new(CompressorControl.Regulated, "control.inverter"),
        new(CompressorControl.OnOff, "control.onOff"),
    };

    public static IReadOnlyList<Option<AppLanguage>> Languages { get; } = new Option<AppLanguage>[]
    {
        new(AppLanguage.English, "language.english"),
        new(AppLanguage.Ukrainian, "language.ukrainian"),
    };

    public static string Source(HeatSource source) => Localizer.T(source switch
    {
        HeatSource.Air => "source.air",
        HeatSource.Brine => "source.brine",
        _ => "source.water",
    });

    public static string Control(CompressorControl control) =>
        Localizer.T(control == CompressorControl.Regulated ? "control.inverter" : "control.onOff");

    public static string Category(MaterialCategory category) => Localizer.T(category switch
    {
        MaterialCategory.Masonry => "category.masonry",
        MaterialCategory.Concrete => "category.concrete",
        MaterialCategory.Timber => "category.timber",
        MaterialCategory.Insulation => "category.insulation",
        _ => "category.finish",
    });
}
