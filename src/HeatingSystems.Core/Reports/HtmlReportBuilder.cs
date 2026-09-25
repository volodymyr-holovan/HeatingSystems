using System.Net;
using System.Text;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Reports;

/// <summary>Creates a self-contained, printable HTML report of a calculation in the current language.</summary>
public static class HtmlReportBuilder
{
    private static string N(double value, string format = "N0") => value.ToString(format, Localizer.Culture);
    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static string T(string key) => E(Localizer.T(key));

    public static string Build(CalculationOutcome o, string projectName, string dataSource)
    {
        var b = o.Building;
        var lang = Localizer.Language == AppLanguage.Ukrainian ? "uk" : "en";
        var sb = new StringBuilder();
        sb.Append($"""
            <!DOCTYPE html>
            <html lang="{lang}"><head><meta charset="utf-8">
            <title>{T("report.title")}</title>
            """);
        sb.Append("""
            <style>
            body{font-family:"Segoe UI",Arial,sans-serif;color:#1d2433;margin:32px;max-width:1000px}
            h1{font-size:24px;margin:0 0 4px}h2{font-size:18px;margin:28px 0 8px;border-bottom:2px solid #e8590c;padding-bottom:4px}
            .muted{color:#5b6475;font-size:13px}
            table{border-collapse:collapse;width:100%;font-size:13px}th,td{padding:6px 8px;border-bottom:1px solid #e3e8ef;text-align:left}
            th{background:#f5f7fa}td.n,th.n{text-align:right;font-variant-numeric:tabular-nums}
            .kpis{display:flex;gap:12px;flex-wrap:wrap}.kpi{border:1px solid #e3e8ef;border-radius:8px;padding:10px 14px;min-width:170px}
            .kpi b{display:block;font-size:20px}.ok{color:#2b8a3e}.bad{color:#c92a2a}
            @media print{body{margin:0}}
            </style></head><body>
            """);
        sb.Append($"<h1>{E(projectName)}</h1>");
        sb.Append($"<div class=\"muted\">{E(Localizer.F("report.subtitle", o.CalculatedAt, b.Climate.City, b.Climate.ClimateZone))}</div>");

        sb.Append($"<h2>{T("report.keyResults")}</h2><div class=\"kpis\">");
        Kpi(sb, "results.designLoad", $"{N(o.HeatLoss.DesignHeatLoad / 1000, "N2")} {Localizer.T("unit.kW")}");
        Kpi(sb, "results.specificLoad", $"{N(o.HeatLoss.SpecificHeatLoad, "N1")} {Localizer.T("unit.Wm2")}");
        Kpi(sb, "results.spaceHeating", $"{N(o.Demand.SpaceHeating)} {Localizer.T("unit.kWhYear")}");
        Kpi(sb, "results.specificDemand", $"{N(o.Demand.SpecificSpaceHeating)} {Localizer.T("unit.kWhm2Year")}");
        Kpi(sb, "results.hotWater", $"{N(o.Demand.HotWater)} {Localizer.T("unit.kWhYear")}");
        sb.Append("</div>");

        sb.Append($"<h2>{T("report.input")}</h2><table>");
        Row(sb, "report.climate", Localizer.F("report.climateValue", b.Climate.City, b.Climate.DesignTemperature,
            b.Climate.HeatingSeasonDays, b.Climate.HeatingSeasonMeanTemperature));
        Row(sb, "results.degreeDays", $"{N(o.Demand.DegreeDays)} {Localizer.T("unit.Kd")}");
        Row(sb, "building.indoorTemperature", $"{N(b.IndoorTemperature, "N1")} °C");
        Row(sb, "report.areaVolume", $"{N(b.HeatedFloorArea, "N1")} m² / {N(b.HeatedVolume, "N1")} m³");
        Row(sb, "building.emitters", Localizer.F("report.flowTemperature", b.DesignFlowTemperature));
        Row(sb, "report.ventilation", b.MechanicalVentilation
            ? Localizer.F("report.ventilationMechanical", b.HeatRecoveryEfficiency * 100, b.AirTightnessN50)
            : Localizer.F("report.ventilationNatural", b.MinimumAirChangeRate, b.AirTightnessN50));
        sb.Append("</table>");

        sb.Append($"<h2>{T("report.heatLoss")}</h2><table><tr><th>{T("report.element")}</th><th class=\"n\">A, m²</th><th class=\"n\">U, W/(m²·K)</th><th class=\"n\">b</th><th class=\"n\">H, W/K</th><th class=\"n\">Φ, W</th><th class=\"n\">{T("report.share")}</th></tr>");
        foreach (var i in o.HeatLoss.Items)
            sb.Append($"<tr><td>{E(i.Name)}</td><td class=\"n\">{(i.Area > 0 ? N(i.Area, "N1") : "—")}</td><td class=\"n\">{(i.Area > 0 ? N(i.UValue, "N3") : "—")}</td><td class=\"n\">{N(i.TemperatureFactor, "N2")}</td><td class=\"n\">{N(i.Coefficient, "N1")}</td><td class=\"n\">{N(i.DesignLoss)}</td><td class=\"n\">{N(i.DesignLoss / o.HeatLoss.DesignHeatLoad * 100, "N1")} %</td></tr>");
        sb.Append($"<tr><th>{T("report.total")}</th><th></th><th></th><th></th><th></th><th class=\"n\">{N(o.HeatLoss.DesignHeatLoad)}</th><th class=\"n\">100 %</th></tr></table>");

        if (o.EnvelopeChecks.Count > 0)
        {
            sb.Append($"<h2>{T("results.envelopeCheck")}</h2><table><tr><th>{T("report.element")}</th><th class=\"n\">R, m²·K/W</th><th class=\"n\">R<sub>q,min</sub></th><th>{T("report.verdict")}</th></tr>");
            foreach (var c in o.EnvelopeChecks)
                sb.Append($"<tr><td>{E(c.Name)}</td><td class=\"n\">{N(c.ActualResistance, "N2")}</td><td class=\"n\">{N(c.RequiredResistance, "N2")}</td><td class=\"{(c.Complies ? "ok" : "bad")}\">{T(c.Complies ? "check.complies" : "check.fails")}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append($"<h2>{T("report.comparison")}</h2><table><tr><th>{T("comparison.system")}</th><th class=\"n\">{T("comparison.efficiency")}</th><th class=\"n\">{T("comparison.finalEnergy")}</th><th class=\"n\">{T("comparison.fuel")}</th><th class=\"n\">{T("comparison.cost")}</th><th class=\"n\">{T("comparison.co2")}</th></tr>");
        foreach (var s in o.Options)
            sb.Append($"<tr><td>{E(s.Name)}</td><td class=\"n\">{N(s.Efficiency, "N2")}</td><td class=\"n\">{N(s.FinalEnergy)}</td><td class=\"n\">{N(s.FuelQuantity)} {E(s.Carrier.Unit)}</td><td class=\"n\">{N(s.AnnualCost)}</td><td class=\"n\">{N(s.AnnualCo2)}</td></tr>");
        sb.Append("</table>");

        if (o.Recommendations.Count > 0)
        {
            sb.Append($"<h2>{T("report.recommended")}</h2><table><tr><th>#</th><th>{T("recommendation.model")}</th><th>{T("recommendation.type")}</th><th class=\"n\">P35, kW</th><th class=\"n\">{T("recommendation.scop")}</th><th class=\"n\">{T("recommendation.spf")}</th><th class=\"n\">{T("recommendation.coverage")}</th><th class=\"n\">{T("recommendation.cost")}</th></tr>");
            foreach (var r in o.Recommendations.Take(10))
            {
                var hp = r.Result.HeatPump;
                sb.Append($"<tr><td>{r.Rank}</td><td>{E(hp.Manufacturer)}<br><b>{E(hp.Model)}</b></td><td>{E(SourceName(hp.Source))}</td><td class=\"n\">{N(hp.RatedPowerLowTemp, "N1")}</td><td class=\"n\">{N(hp.Scop, "N2")}</td><td class=\"n\">{N(r.Result.OverallSpf, "N2")}</td><td class=\"n\">{N(r.Result.Coverage * 100, "N1")} %</td><td class=\"n\">{N(r.AnnualCost)}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<p class=\"muted\">{E(Localizer.F("report.footer", dataSource))}</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string SourceName(HeatSource source) => Localizer.T(source switch
    {
        HeatSource.Air => "source.air",
        HeatSource.Brine => "source.brine",
        HeatSource.Water => "source.water",
        _ => "source.air",
    });

    private static void Kpi(StringBuilder sb, string titleKey, string value) =>
        sb.Append($"<div class=\"kpi\"><span class=\"muted\">{T(titleKey)}</span><b>{E(value)}</b></div>");

    private static void Row(StringBuilder sb, string nameKey, string value) =>
        sb.Append($"<tr><th style=\"width:40%\">{T(nameKey)}</th><td>{E(value)}</td></tr>");
}
