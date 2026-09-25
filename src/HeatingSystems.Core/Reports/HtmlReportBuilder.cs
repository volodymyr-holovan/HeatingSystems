using System.Globalization;
using System.Net;
using System.Text;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Reports;

/// <summary>Creates a self-contained, printable HTML report of a calculation.</summary>
public static class HtmlReportBuilder
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static string N(double value, string format = "N0") => value.ToString(format, Uk);
    private static string E(string text) => WebUtility.HtmlEncode(text);

    public static string Build(CalculationOutcome o, string projectName, string dataSource)
    {
        var b = o.Building;
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="uk"><head><meta charset="utf-8">
            <title>Звіт — системи опалення</title>
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
        sb.Append($"<div class=\"muted\">Розрахунок від {o.CalculatedAt.ToString("dd.MM.yyyy HH:mm", Uk)} · {E(b.Climate.City)} · температурна зона {b.Climate.ClimateZone}</div>");

        sb.Append("<h2>Основні результати</h2><div class=\"kpis\">");
        Kpi(sb, "Розрахункове теплове навантаження", $"{N(o.HeatLoss.DesignHeatLoad / 1000, "N2")} кВт");
        Kpi(sb, "Питоме навантаження", $"{N(o.HeatLoss.SpecificHeatLoad, "N1")} Вт/м²");
        Kpi(sb, "Потреба на опалення", $"{N(o.Demand.SpaceHeating)} кВт·год/рік");
        Kpi(sb, "Питома потреба", $"{N(o.Demand.SpecificSpaceHeating)} кВт·год/(м²·рік)");
        Kpi(sb, "Гаряче водопостачання", $"{N(o.Demand.HotWater)} кВт·год/рік");
        sb.Append("</div>");

        sb.Append("<h2>Вихідні дані</h2><table>");
        Row(sb, "Місто / θe / тривалість опал. періоду", $"{E(b.Climate.City)} / {N(b.Climate.DesignTemperature, "N0")} °C / {b.Climate.HeatingSeasonDays} діб (θср = {N(b.Climate.HeatingSeasonMeanTemperature, "N1")} °C)");
        Row(sb, "Градусо-доби опалювального періоду", $"{N(o.Demand.DegreeDays)} К·діб");
        Row(sb, "Внутрішня температура", $"{N(b.IndoorTemperature, "N1")} °C");
        Row(sb, "Опалювана площа / об'єм", $"{N(b.HeatedFloorArea, "N1")} м² / {N(b.HeatedVolume, "N1")} м³");
        Row(sb, "Опалювальні прилади", $"розрахункова температура подачі {N(b.DesignFlowTemperature)} °C");
        Row(sb, "Вентиляція", b.MechanicalVentilation
            ? $"механічна, рекуперація {N(b.HeatRecoveryEfficiency * 100)} %, n50 = {N(b.AirTightnessN50, "N1")} год⁻¹"
            : $"природна, n = {N(b.MinimumAirChangeRate, "N2")} год⁻¹, n50 = {N(b.AirTightnessN50, "N1")} год⁻¹");
        sb.Append("</table>");

        sb.Append("<h2>Тепловтрати (EN 12831)</h2><table><tr><th>Елемент</th><th class=\"n\">A, м²</th><th class=\"n\">U, Вт/(м²·К)</th><th class=\"n\">b</th><th class=\"n\">H, Вт/К</th><th class=\"n\">Φ, Вт</th><th class=\"n\">Частка</th></tr>");
        foreach (var i in o.HeatLoss.Items)
            sb.Append($"<tr><td>{E(i.Name)}</td><td class=\"n\">{(i.Area > 0 ? N(i.Area, "N1") : "—")}</td><td class=\"n\">{(i.Area > 0 ? N(i.UValue, "N3") : "—")}</td><td class=\"n\">{N(i.TemperatureFactor, "N2")}</td><td class=\"n\">{N(i.Coefficient, "N1")}</td><td class=\"n\">{N(i.DesignLoss)}</td><td class=\"n\">{N(i.DesignLoss / o.HeatLoss.DesignHeatLoad * 100, "N1")} %</td></tr>");
        sb.Append($"<tr><th>Разом</th><th></th><th></th><th></th><th></th><th class=\"n\">{N(o.HeatLoss.DesignHeatLoad)}</th><th class=\"n\">100 %</th></tr></table>");

        if (o.EnvelopeChecks.Count > 0)
        {
            sb.Append("<h2>Відповідність ДБН В.2.6-31</h2><table><tr><th>Елемент</th><th class=\"n\">R, м²·К/Вт</th><th class=\"n\">Rq,min</th><th>Висновок</th></tr>");
            foreach (var c in o.EnvelopeChecks)
                sb.Append($"<tr><td>{E(c.Name)}</td><td class=\"n\">{N(c.ActualResistance, "N2")}</td><td class=\"n\">{N(c.RequiredResistance, "N2")}</td><td class=\"{(c.Complies ? "ok" : "bad")}\">{(c.Complies ? "відповідає" : "не відповідає")}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<h2>Порівняння систем опалення (опалення + ГВП)</h2><table><tr><th>Система</th><th class=\"n\">ККД / SPF</th><th class=\"n\">Кінцева енергія, кВт·год</th><th class=\"n\">Паливо</th><th class=\"n\">Витрати, грн/рік</th><th class=\"n\">CO₂, кг/рік</th></tr>");
        foreach (var s in o.Options)
            sb.Append($"<tr><td>{E(s.Name)}</td><td class=\"n\">{N(s.Efficiency, "N2")}</td><td class=\"n\">{N(s.FinalEnergy)}</td><td class=\"n\">{N(s.FuelQuantity)} {E(s.Carrier.Unit)}</td><td class=\"n\">{N(s.AnnualCost)}</td><td class=\"n\">{N(s.AnnualCo2)}</td></tr>");
        sb.Append("</table>");

        if (o.Recommendations.Count > 0)
        {
            sb.Append("<h2>Рекомендовані теплові насоси</h2><table><tr><th>#</th><th>Виробник / модель</th><th>Джерело</th><th class=\"n\">P35, кВт</th><th class=\"n\">SCOP (серт.)</th><th class=\"n\">SPF розрах.</th><th class=\"n\">Покриття</th><th class=\"n\">Грн/рік</th></tr>");
            foreach (var r in o.Recommendations.Take(10))
            {
                var hp = r.Result.HeatPump;
                sb.Append($"<tr><td>{r.Rank}</td><td>{E(hp.Manufacturer)}<br><b>{E(hp.Model)}</b></td><td>{SourceName(hp.Source)}</td><td class=\"n\">{N(hp.RatedPowerLowTemp, "N1")}</td><td class=\"n\">{N(hp.Scop, "N2")}</td><td class=\"n\">{N(r.Result.OverallSpf, "N2")}</td><td class=\"n\">{N(r.Result.Coverage * 100, "N1")} %</td><td class=\"n\">{N(r.AnnualCost)}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<p class=\"muted\">Каталог: {E(dataSource)}. Кліматичні параметри, тарифи та коефіцієнти викидів є довідковими; перед використанням у проєктній документації звірте їх з чинними нормативами. Методика: EN 12831, EN ISO 6946, EN ISO 13370, EN ISO 13790, EN 14825.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string SourceName(HeatSource source) => source switch
    {
        HeatSource.Air => "повітря–вода",
        HeatSource.Brine => "ґрунт–вода",
        HeatSource.Water => "вода–вода",
        _ => source.ToString(),
    };

    private static void Kpi(StringBuilder sb, string title, string value) =>
        sb.Append($"<div class=\"kpi\"><span class=\"muted\">{E(title)}</span><b>{E(value)}</b></div>");

    private static void Row(StringBuilder sb, string name, string value) =>
        sb.Append($"<tr><th style=\"width:40%\">{E(name)}</th><td>{value}</td></tr>");
}
