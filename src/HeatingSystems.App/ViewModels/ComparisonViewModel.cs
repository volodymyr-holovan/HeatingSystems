using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeatingSystems.Core.Calculations;

namespace HeatingSystems.App.ViewModels;

public sealed record ComparisonRow(
    int Rank,
    string Name,
    bool IsHeatPump,
    double Efficiency,
    double FinalEnergy,
    string Fuel,
    double AnnualCost,
    double AnnualCo2,
    double CostFraction,
    double Co2Fraction,
    double MonthlyCost,
    string Notes);

/// <summary>Running costs and emissions of all heat generators (page "Порівняння").</summary>
public sealed partial class ComparisonViewModel() : PageViewModel("Порівняння", "\uE8EF", "Експлуатаційні витрати та викиди CO₂")
{
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "";

    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    public void Update(CalculationOutcome? outcome)
    {
        Rows.Clear();
        HasResults = outcome is not null && outcome.Options.Count > 0;
        if (!HasResults) { Summary = ""; return; }

        var options = outcome!.Options;
        var maxCost = options.Max(o => o.AnnualCost);
        var maxCo2 = options.Max(o => o.AnnualCo2);
        var rank = 1;
        foreach (var o in options)
            Rows.Add(new ComparisonRow(rank++, o.Name, o.Kind == SystemKind.HeatPump, o.Efficiency, o.FinalEnergy,
                $"{o.FuelQuantity:N0} {o.Carrier.Unit}", o.AnnualCost, o.AnnualCo2,
                maxCost > 0 ? o.AnnualCost / maxCost : 0, maxCo2 > 0 ? o.AnnualCo2 / maxCo2 : 0,
                o.AnnualCost / 12, o.Notes));

        var best = options[0];
        var worst = options[^1];
        Summary = $"Найдешевша в експлуатації: {best.Name} — {best.AnnualCost:N0} грн/рік " +
                  $"(на {worst.AnnualCost - best.AnnualCost:N0} грн менше, ніж {worst.Name}). " +
                  $"Потреба в теплоті: {outcome.Demand.Total:N0} кВт·год/рік (опалення + ГВП).";
    }
}
