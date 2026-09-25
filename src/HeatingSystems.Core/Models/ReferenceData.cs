using HeatingSystems.Core.Localization;

namespace HeatingSystems.Core.Models;

/// <summary>Building material with its design thermal conductivity λ.</summary>
/// <param name="Conductivity">Design thermal conductivity λ, W/(m·K).</param>
/// <param name="Density">Density, kg/m³ (informational).</param>
public sealed record Material(
    int Id,
    string NameEn,
    string NameUk,
    MaterialCategory Category,
    double Conductivity,
    double Density,
    string Source)
{
    /// <summary>Name in the current UI language.</summary>
    public string Name => Localizer.Pick(NameEn, NameUk);

    public override string ToString() => Name;
}

public enum MaterialCategory
{
    Masonry = 1,
    Concrete = 2,
    Timber = 3,
    Insulation = 4,
    Finish = 5,
}

/// <summary>Window or glazed door type.</summary>
/// <param name="UValue">Overall window thermal transmittance U_w, W/(m²·K).</param>
/// <param name="GValue">Total solar energy transmittance of glazing, –.</param>
public sealed record WindowType(int Id, string NameEn, string NameUk, double UValue, double GValue, string Source)
{
    public string Name => Localizer.Pick(NameEn, NameUk);

    public override string ToString() => Name;
}

/// <summary>
/// Climate data of a location: design outdoor temperature and heating season statistics.
/// </summary>
/// <param name="DesignTemperature">Design outdoor temperature θ_e (coldest five-day period), °C.</param>
/// <param name="HeatingSeasonDays">Length of the heating season (days with mean temperature ≤ 8 °C).</param>
/// <param name="HeatingSeasonMeanTemperature">Mean outdoor temperature of the heating season, °C.</param>
/// <param name="AnnualMeanTemperature">Annual mean outdoor temperature θ_m,e, °C.</param>
public sealed record ClimateLocation(
    int Id,
    string CityEn,
    string CityUk,
    string RegionEn,
    string RegionUk,
    double DesignTemperature,
    int HeatingSeasonDays,
    double HeatingSeasonMeanTemperature,
    double AnnualMeanTemperature,
    string Source)
{
    public string City => Localizer.Pick(CityEn, CityUk);
    public string Region => Localizer.Pick(RegionEn, RegionUk);
    public double HeatingSeasonHours => HeatingSeasonDays * 24.0;

    /// <summary>Heating degree-days (K·day) for the given indoor temperature.</summary>
    public double DegreeDays(double indoorTemperature) =>
        HeatingSeasonDays * (indoorTemperature - HeatingSeasonMeanTemperature);

    /// <summary>
    /// Temperature zone of Ukraine according to DBN V.2.6-31:2016 (degree-days at θ_i = 20 °C):
    /// zone I — more than 3500 K·day, zone II — up to 3500 K·day.
    /// </summary>
    public int ClimateZone => DegreeDays(20.0) > 3500 ? 1 : 2;

    public override string ToString() => City;
}

/// <summary>Final energy carrier (fuel, electricity, district heat) with tariff and emission factor.</summary>
/// <param name="EnergyPerUnit">Net calorific value / energy content, kWh per <see cref="Unit"/>.</param>
/// <param name="PricePerUnit">Price per <see cref="Unit"/>, UAH.</param>
/// <param name="Co2PerKWh">CO₂ emission factor, kg/kWh of final energy.</param>
public sealed record EnergyCarrier(
    string Code,
    string NameEn,
    string NameUk,
    string UnitEn,
    string UnitUk,
    double EnergyPerUnit,
    double PricePerUnit,
    double Co2PerKWh,
    string Source)
{
    public string Name => Localizer.Pick(NameEn, NameUk);
    public string Unit => Localizer.Pick(UnitEn, UnitUk);
    public double PricePerKWh => EnergyPerUnit > 0 ? PricePerUnit / EnergyPerUnit : 0;

    public override string ToString() => Name;
}

/// <summary>Combustion / direct heat generator technology with a seasonal efficiency.</summary>
/// <param name="SeasonalEfficiency">Seasonal generation efficiency referred to net calorific value, –.</param>
/// <param name="LowTemperatureBonus">
/// Additional efficiency for low-temperature systems (condensing boilers), applied linearly between
/// 70 °C (0) and 35 °C (full bonus) design flow temperature.
/// </param>
public sealed record HeatingTechnology(
    string Code,
    string NameEn,
    string NameUk,
    string CarrierCode,
    double SeasonalEfficiency,
    double LowTemperatureBonus,
    string DescriptionEn,
    string DescriptionUk,
    string Source)
{
    public string Name => Localizer.Pick(NameEn, NameUk);
    public string Description => Localizer.Pick(DescriptionEn, DescriptionUk);

    public double EfficiencyAt(double designFlowTemperature)
    {
        if (LowTemperatureBonus <= 0) return SeasonalEfficiency;
        var share = Math.Clamp((70.0 - designFlowTemperature) / 35.0, 0.0, 1.0);
        return SeasonalEfficiency + LowTemperatureBonus * share;
    }

    public override string ToString() => Name;
}

/// <summary>Minimum thermal resistance requirement for an envelope element in a climate zone.</summary>
public sealed record EnvelopeRequirement(int ClimateZone, EnvelopeElement Element, double MinThermalResistance, string Source);

public enum EnvelopeElement
{
    Wall = 1,
    /// <summary>Combined (exposed) roof.</summary>
    Roof = 2,
    /// <summary>Ceiling under an unheated attic.</summary>
    AtticFloor = 3,
    /// <summary>Floor above an unheated basement.</summary>
    BasementFloor = 4,
    /// <summary>Floor above outdoor air (passages).</summary>
    ExposedFloor = 5,
    Window = 6,
}
