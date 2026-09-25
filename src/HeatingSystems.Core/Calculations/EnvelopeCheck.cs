using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>Result of comparing an element's thermal resistance with the normative minimum.</summary>
public sealed record EnvelopeCheckItem(EnvelopeElement Element, string Name, double ActualResistance, double RequiredResistance)
{
    public bool Complies => ActualResistance + 1e-9 >= RequiredResistance;
}

/// <summary>Checks the envelope against minimum thermal resistances (ДБН В.2.6-31) of the climate zone.</summary>
public static class EnvelopeCheck
{
    public static IReadOnlyList<EnvelopeCheckItem> Check(BuildingInput b, IEnumerable<EnvelopeRequirement> requirements)
    {
        var zone = b.Climate.ClimateZone;
        var req = requirements.Where(r => r.ClimateZone == zone).ToDictionary(r => r.Element);
        var items = new List<EnvelopeCheckItem>();

        if (b.NetWallArea > 0 && req.TryGetValue(EnvelopeElement.Wall, out var w))
            items.Add(new(EnvelopeElement.Wall, "Зовнішні стіни",
                ThermalTransmittance.TotalResistance(b.Wall, HeatFlowDirection.Horizontal), w.MinThermalResistance));
        if (b.RoofArea > 0)
        {
            var attic = b.RoofType == RoofType.UnheatedAttic;
            if (req.TryGetValue(attic ? EnvelopeElement.AtticFloor : EnvelopeElement.Roof, out var r))
                items.Add(new(r.Element, attic ? "Горищне перекриття" : "Суміщене покриття",
                    ThermalTransmittance.TotalResistance(b.Roof, HeatFlowDirection.Upwards, attic), r.MinThermalResistance));
        }
        if (b.FloorArea > 0 && b.FloorType != FloorType.SlabOnGround)
        {
            var basement = b.FloorType == FloorType.AboveUnheatedBasement;
            if (req.TryGetValue(basement ? EnvelopeElement.BasementFloor : EnvelopeElement.ExposedFloor, out var f))
                items.Add(new(f.Element, basement ? "Перекриття над неопалюваним підвалом" : "Перекриття над проїздом",
                    ThermalTransmittance.TotalResistance(b.Floor, HeatFlowDirection.Downwards, basement), f.MinThermalResistance));
        }
        if (b.WindowArea > 0 && b.Window is not null && req.TryGetValue(EnvelopeElement.Window, out var win))
            items.Add(new(EnvelopeElement.Window, "Вікна", 1.0 / b.Window.UValue, win.MinThermalResistance));
        return items;
    }
}
