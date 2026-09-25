using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeatingSystems.Core.Calculations;

using HeatingSystems.Core.Localization;

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

/// <summary>Running costs and emissions of all heat generators (page "Comparison").</summary>
public sealed partial class ComparisonViewModel() : PageViewModel("page.comparison", "\uE8EF", "page.comparison.subtitle")
{
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "";

    private CalculationOutcome? _outcome;

    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    public override void RefreshLanguage()
    {
        Update(_outcome);
        base.RefreshLanguage();
    }

    public void Update(CalculationOutcome? outcome)
    {
        _outcome = outcome;
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
        Summary = Localizer.F("comparison.summary", best.Name, best.AnnualCost, worst.AnnualCost - best.AnnualCost, worst.Name,
            outcome.Demand.Total);
    }
}
