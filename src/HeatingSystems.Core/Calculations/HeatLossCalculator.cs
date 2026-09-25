using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>
/// Design heat load of a building treated as a single zone according to EN 12831
/// (simplified thermal bridge method, ground losses per EN ISO 13370).
/// </summary>
public static class HeatLossCalculator
{
    /// <summary>Volumetric heat capacity of air ρ·c_p, W·h/(m³·K).</summary>
    public const double AirHeatCapacity = 0.34;

    /// <summary>Correction for the annual variation of the outdoor temperature f_g1 (EN 12831, D.4.3).</summary>
    public const double GroundAnnualVariationFactor = 1.45;

    /// <summary>Temperature correction factor b for a ceiling under an unheated attic (EN 12831, table D.3).</summary>
    public const double AtticTemperatureFactor = 0.9;

    /// <summary>Temperature correction factor b for a floor above an unheated basement (EN 12831, table D.3).</summary>
    public const double BasementTemperatureFactor = 0.6;

    /// <summary>Minimum wall thickness assumed for ground calculations when no wall layers are given, m.</summary>
    private const double DefaultWallThickness = 0.3;

    public static HeatLossResult Calculate(BuildingInput b)
    {
        var errors = b.Validate().ToList();
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(b));

        var dT = b.IndoorTemperature - b.Climate.DesignTemperature;
        var tb = b.ThermalBridgeSurcharge;
        var items = new List<HeatLossItem>();

        void AddTransmission(string name, double area, double u, double factor)
        {
            if (area <= 0) return;
            var uc = u + tb;
            var h = area * uc * factor;
            items.Add(new HeatLossItem(name, LossCategory.Transmission, area, uc, factor, h, h * dT));
        }

        AddTransmission("Зовнішні стіни", b.NetWallArea, ThermalTransmittance.UValue(b.Wall, HeatFlowDirection.Horizontal), 1.0);
        if (b.Window is not null) AddTransmission("Вікна", b.WindowArea, b.Window.UValue, 1.0);
        AddTransmission("Зовнішні двері", b.DoorArea, b.DoorUValue, 1.0);

        var atticRoof = b.RoofType == RoofType.UnheatedAttic;
        AddTransmission(
            atticRoof ? "Перекриття під холодним горищем" : "Покрівля",
            b.RoofArea,
            ThermalTransmittance.UValue(b.Roof, HeatFlowDirection.Upwards, adjoinsUnheatedSpace: atticRoof),
            atticRoof ? AtticTemperatureFactor : 1.0);

        double hGround = 0, hPeriodic = 0;
        switch (b.FloorType)
        {
            case FloorType.SlabOnGround when b.FloorArea > 0:
            {
                var w = b.Wall.Thickness > 0 ? b.Wall.Thickness : DefaultWallThickness;
                var u = GroundHeatTransfer.SlabUValue(b.FloorArea, b.FloorPerimeter, w, b.Floor);
                hGround = b.FloorArea * u;
                hPeriodic = GroundHeatTransfer.PeriodicCoefficient(b.FloorPerimeter, w, b.Floor);
                // EN 12831 D.4.3: Φ = f_g1·f_g2·A·U_equiv·G_w·(θ_int − θ_e), f_g2 = (θ_int − θ_m,e)/(θ_int − θ_e), G_w = 1.
                var fg2 = (b.IndoorTemperature - b.Climate.AnnualMeanTemperature) / dT;
                var factor = GroundAnnualVariationFactor * fg2;
                items.Add(new HeatLossItem("Підлога по ґрунту", LossCategory.Ground, b.FloorArea, u, factor,
                    hGround * factor, hGround * factor * dT));
                break;
            }
            case FloorType.AboveUnheatedBasement:
                AddTransmission("Перекриття над неопалюваним підвалом", b.FloorArea,
                    ThermalTransmittance.UValue(b.Floor, HeatFlowDirection.Downwards, adjoinsUnheatedSpace: true),
                    BasementTemperatureFactor);
                break;
            case FloorType.AboveOutdoorAir:
                AddTransmission("Підлога над зовнішнім повітрям", b.FloorArea,
                    ThermalTransmittance.UValue(b.Floor, HeatFlowDirection.Downwards), 1.0);
                break;
        }

        var airFlow = EffectiveAirFlow(b);
        var hV = AirHeatCapacity * airFlow;
        items.Add(new HeatLossItem(
            b.MechanicalVentilation ? "Вентиляція (з рекуперацією) та інфільтрація" : "Вентиляція та інфільтрація",
            LossCategory.Ventilation, 0, 0, 1.0, hV, hV * dT));

        return new HeatLossResult
        {
            Items = items,
            TransmissionCoefficient = items.Where(i => i.Category == LossCategory.Transmission).Sum(i => i.Coefficient),
            VentilationCoefficient = hV,
            EffectiveAirFlow = airFlow,
            GroundCoefficient = hGround,
            GroundPeriodicCoefficient = hPeriodic,
            DesignTemperatureDifference = dT,
            SpecificHeatLoad = items.Sum(i => i.DesignLoss) / b.HeatedFloorArea,
        };
    }

    /// <summary>
    /// Effective air flow, m³/h (EN 12831, 7.2 for the whole building):
    /// infiltration V_inf = V·n50·e·ε (ε = 1, building height &lt; 10 m);
    /// natural ventilation: V = max(V_inf, V_min);
    /// balanced mechanical ventilation: V = V_inf + V_min·(1 − η_v).
    /// </summary>
    public static double EffectiveAirFlow(BuildingInput b)
    {
        var infiltration = b.HeatedVolume * b.AirTightnessN50 * b.ShieldingCoefficient;
        var minimum = b.HeatedVolume * b.MinimumAirChangeRate;
        return b.MechanicalVentilation
            ? infiltration + minimum * (1 - b.HeatRecoveryEfficiency)
            : Math.Max(infiltration, minimum);
    }
}
