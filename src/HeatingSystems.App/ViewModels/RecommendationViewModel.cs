using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.App.Services;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;
using HeatingSystems.Core.Recommendations;

namespace HeatingSystems.App.ViewModels;

public sealed class RecommendationRow(Recommendation recommendation)
{
    public Recommendation Source { get; } = recommendation;
    private Recommendation R => Source;
    public int Rank => R.Rank;
    public HeatPumpModel HeatPump => R.Result.HeatPump;
    public string Manufacturer => HeatPump.Manufacturer;
    public string Model => HeatPump.Model;
    public string SourceName => DisplayNames.Source(HeatPump.Source);
    public string ControlName => DisplayNames.Control(HeatPump.Control);
    public double RatedPower => HeatPump.RatedPowerLowTemp;
    public double CertifiedScop => HeatPump.Scop;
    public double SpaceHeatingSpf => R.Result.SpaceHeatingSpf;
    public double OverallSpf => R.Result.OverallSpf;
    public double Coverage => R.Result.Coverage;
    public double CapacityAtDesignKw => R.Result.CapacityAtDesign / 1000;
    public string Bivalent => R.Result.BivalentTemperature is { } t ? $"{t:0} °C" : "—";
    public double BackupHours => R.Result.BackupHours;
    public double Electricity => R.Result.TotalElectricity;
    public double AnnualCost => R.AnnualCost;
    public double AnnualCo2 => R.AnnualCo2;
    public string Noise => HeatPump.SoundPowerOutdoor is { } n ? $"{n:0} дБ(А)" : "н/д";
    public string Refrigerant => HeatPump.Refrigerant;
    public string Variants => R.Variants.Count == 0 ? "" : "Ідентичні моделі: " + string.Join("; ", R.Variants);
}

/// <summary>Heat pump selection (page "Підбір ТН").</summary>
public sealed partial class RecommendationViewModel : PageViewModel
{
    private readonly ICatalogRepository _catalog;
    private readonly IDialogService _dialogs;
    private CalculationOutcome? _outcome;

    public RecommendationViewModel(ICatalogRepository catalog, IDialogService dialogs)
        : base("Підбір ТН", "\uE945", "Теплові насоси з каталогу, змодельовані для вашої будівлі")
    {
        _catalog = catalog;
        _dialogs = dialogs;
    }

    [ObservableProperty] private bool _useAir = true;
    [ObservableProperty] private bool _useBrine = true;
    [ObservableProperty] private bool _useWater = true;
    [ObservableProperty] private bool _naturalRefrigerantsOnly;
    [ObservableProperty] private double _maxNoise;
    [ObservableProperty] private double _minCoveragePercent = 95;
    [ObservableProperty] private double _maxSizingRatio = 2.0;
    [ObservableProperty] private int _count = 20;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Спочатку виконайте розрахунок будівлі.";
    [ObservableProperty] private RecommendationRow? _selected;

    public ObservableCollection<RecommendationRow> Rows { get; } = new();

    public RecommendationOptions BuildOptions()
    {
        var sources = new List<HeatSource>();
        if (UseAir) sources.Add(HeatSource.Air);
        if (UseBrine) sources.Add(HeatSource.Brine);
        if (UseWater) sources.Add(HeatSource.Water);
        return new RecommendationOptions
        {
            Sources = sources,
            NaturalRefrigerantsOnly = NaturalRefrigerantsOnly,
            MaxOutdoorSoundPower = MaxNoise > 0 ? MaxNoise : null,
            MinCoverage = Math.Clamp(MinCoveragePercent / 100.0, 0, 1),
            MaxSizingRatio = Math.Max(1.0, MaxSizingRatio),
            Count = Math.Clamp(Count, 1, 200),
        };
    }

    /// <summary>Shows the recommendations computed together with the building calculation.</summary>
    public void Update(CalculationOutcome? outcome)
    {
        _outcome = outcome;
        Show(outcome?.Recommendations ?? Array.Empty<Recommendation>());
    }

    [RelayCommand]
    private async Task RecommendAsync()
    {
        if (_outcome is null)
        {
            _dialogs.ShowInfo("Спочатку виконайте розрахунок будівлі на сторінці «Будівля».");
            return;
        }
        var options = BuildOptions();
        if (options.Sources.Count == 0)
        {
            _dialogs.ShowInfo("Оберіть хоча б одне джерело теплоти.");
            return;
        }

        var carriers = _catalog.GetEnergyCarriers();
        var electricity = carriers.FirstOrDefault(c => c.Code == SystemComparison.ElectricityCode);
        if (electricity is null)
        {
            _dialogs.ShowError("У довіднику немає тарифу на електроенергію.");
            return;
        }

        IsBusy = true;
        Status = "Моделювання теплових насосів…";
        try
        {
            var o = _outcome;
            var list = await Task.Run(() => new HeatPumpRecommender(_catalog).Recommend(o.Building, o.HeatLoss, o.Demand, electricity, options));
            Show(list);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Помилка підбору: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(IReadOnlyList<Recommendation> list)
    {
        Rows.Clear();
        foreach (var r in list) Rows.Add(new RecommendationRow(r));
        HasResults = Rows.Count > 0;
        Selected = Rows.FirstOrDefault();
        Status = _outcome is null
            ? "Спочатку виконайте розрахунок будівлі."
            : Rows.Count == 0
                ? "Немає моделей, що відповідають умовам. Послабте обмеження (шум, покриття, джерело)."
                : $"Знайдено {Rows.Count} варіантів для навантаження {_outcome.HeatLoss.DesignHeatLoad / 1000:0.0} кВт. " +
                  "Рейтинг — за річними витратами на електроенергію; вартість обладнання та буріння свердловин не враховано.";
    }
}
