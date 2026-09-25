using HeatingSystems.Core.Models;

namespace HeatingSystems.App.ViewModels;

/// <summary>Ukrainian names for enumerations.</summary>
public static class DisplayNames
{
    public static IReadOnlyList<Option<RoofType>> RoofTypes { get; } = new Option<RoofType>[]
    {
        new(RoofType.UnheatedAttic, "Перекриття під холодним горищем"),
        new(RoofType.ExposedRoof, "Суміщене покриття (плоский / мансардний дах)"),
    };

    public static IReadOnlyList<Option<FloorType>> FloorTypes { get; } = new Option<FloorType>[]
    {
        new(FloorType.SlabOnGround, "Підлога по ґрунту"),
        new(FloorType.AboveUnheatedBasement, "Над неопалюваним підвалом"),
        new(FloorType.AboveOutdoorAir, "Над проїздом (зовнішнє повітря)"),
    };

    public static IReadOnlyList<Option<EmitterType>> Emitters { get; } = new Option<EmitterType>[]
    {
        new(EmitterType.Underfloor, "Тепла підлога (35 °C)"),
        new(EmitterType.LowTemperatureRadiators, "Низькотемпературні радіатори / фанкойли (45 °C)"),
        new(EmitterType.Radiators, "Панельні радіатори (55 °C)"),
        new(EmitterType.HighTemperatureRadiators, "Старі чавунні радіатори (70 °C)"),
    };

    public static IReadOnlyList<Option<ThermalMassClass>> ThermalMasses { get; } = new Option<ThermalMassClass>[]
    {
        new(ThermalMassClass.Light, "Легка (каркас, дерево)"),
        new(ThermalMassClass.Medium, "Середня"),
        new(ThermalMassClass.Heavy, "Масивна (цегла, бетон)"),
    };

    public static IReadOnlyList<Option<double>> Shielding { get; } = new Option<double>[]
    {
        new(0.05, "Відкрита місцевість (e = 0,05)"),
        new(0.03, "Помірне затінення (e = 0,03)"),
        new(0.02, "Щільна забудова (e = 0,02)"),
    };

    public static IReadOnlyList<Option<HeatSource?>> Sources { get; } = new Option<HeatSource?>[]
    {
        new(null, "Усі джерела"),
        new(HeatSource.Air, "Повітря–вода"),
        new(HeatSource.Brine, "Ґрунт–вода (розсіл)"),
        new(HeatSource.Water, "Вода–вода"),
    };

    public static IReadOnlyList<Option<CompressorControl?>> Controls { get; } = new Option<CompressorControl?>[]
    {
        new(null, "Будь-яке керування"),
        new(CompressorControl.Regulated, "Інверторні"),
        new(CompressorControl.OnOff, "Вкл./викл."),
    };

    public static string Source(HeatSource source) => source switch
    {
        HeatSource.Air => "Повітря–вода",
        HeatSource.Brine => "Ґрунт–вода",
        HeatSource.Water => "Вода–вода",
        _ => source.ToString(),
    };

    public static string Control(CompressorControl control) =>
        control == CompressorControl.Regulated ? "Інвертор" : "Вкл./викл.";

    public static string Category(MaterialCategory category) => category switch
    {
        MaterialCategory.Masonry => "Кладка",
        MaterialCategory.Concrete => "Бетон",
        MaterialCategory.Timber => "Деревина",
        MaterialCategory.Insulation => "Утеплювач",
        MaterialCategory.Finish => "Оздоблення",
        _ => category.ToString(),
    };
}
