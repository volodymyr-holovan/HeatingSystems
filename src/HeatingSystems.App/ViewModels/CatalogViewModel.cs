using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;

namespace HeatingSystems.App.ViewModels;

public sealed record CopPoint(string Condition, double Cop, double MaxOutputKw);

/// <summary>Searchable heat pump catalogue (page "Каталог").</summary>
public sealed partial class CatalogViewModel : PageViewModel
{
    private const int PageSize = 200;
    private readonly ICatalogRepository _catalog;
    private bool _loaded;

    public CatalogViewModel(ICatalogRepository catalog)
        : base("Каталог", "\uE8F1", "Сертифіковані теплові насоси Heat Pump KEYMARK")
    {
        _catalog = catalog;
        Sort = SortOptions[0];
        Source = DisplayNames.Sources[0];
        Control = DisplayNames.Controls[0];
    }

    public IReadOnlyList<Option<HeatSource?>> SourceOptions => DisplayNames.Sources;
    public IReadOnlyList<Option<CompressorControl?>> ControlOptions => DisplayNames.Controls;
    public IReadOnlyList<Option<HeatPumpSortOrder>> SortOptions { get; } = new Option<HeatPumpSortOrder>[]
    {
        new(HeatPumpSortOrder.Manufacturer, "За виробником"),
        new(HeatPumpSortOrder.ScopDescending, "За SCOP (спадання)"),
        new(HeatPumpSortOrder.PowerAscending, "За потужністю (зростання)"),
        new(HeatPumpSortOrder.PowerDescending, "За потужністю (спадання)"),
        new(HeatPumpSortOrder.NoiseAscending, "За шумом (тихіші спочатку)"),
    };

    public ObservableCollection<string> Manufacturers { get; } = new();
    public ObservableCollection<string> Refrigerants { get; } = new();
    public ObservableCollection<HeatPumpModel> Items { get; } = new();
    public ObservableCollection<CopPoint> CopPoints { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _manufacturer;
    [ObservableProperty] private Option<HeatSource?>? _source;
    [ObservableProperty] private Option<CompressorControl?>? _control;
    [ObservableProperty] private string? _refrigerant;
    [ObservableProperty] private double _minPower;
    [ObservableProperty] private double _maxPower;
    [ObservableProperty] private double _minScop;
    [ObservableProperty] private double _maxNoise;
    [ObservableProperty] private Option<HeatPumpSortOrder>? _sort;
    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _statistics = "";
    [ObservableProperty] private HeatPumpModel? _selected;
    [ObservableProperty] private double _modelScop;

    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public string PageText => $"Сторінка {PageIndex + 1} з {PageCount} · знайдено {TotalCount:N0}";
    public string SelectedSource => Selected is { } s ? DisplayNames.Source(s.Source) : "";
    public string SelectedControl => Selected is { } s ? DisplayNames.Control(s.Control) : "";

    /// <summary>Loads lists and the first page on first navigation.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Manufacturers.Clear();
        foreach (var m in _catalog.GetManufacturers()) Manufacturers.Add(m);
        Refrigerants.Clear();
        foreach (var r in _catalog.GetRefrigerants()) Refrigerants.Add(r);
        var s = _catalog.GetStatistics();
        Statistics = $"{s.HeatPumpCount:N0} моделей від {s.ManufacturerCount} виробників · медіанний SCOP {s.MedianScop:0.00} · " +
                     $"повітря–вода {s.BySource.GetValueOrDefault(HeatSource.Air):N0}, ґрунт–вода {s.BySource.GetValueOrDefault(HeatSource.Brine):N0}, " +
                     $"вода–вода {s.BySource.GetValueOrDefault(HeatSource.Water):N0}";
        Load();
    }

    [RelayCommand]
    private void Search()
    {
        PageIndex = 0;
        Load();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = "";
        Manufacturer = null;
        Source = DisplayNames.Sources[0];
        Control = DisplayNames.Controls[0];
        Refrigerant = null;
        MinPower = MaxPower = MinScop = MaxNoise = 0;
        Sort = SortOptions[0];
        Search();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (PageIndex + 1 >= PageCount) return;
        PageIndex++;
        Load();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (PageIndex == 0) return;
        PageIndex--;
        Load();
    }

    private void Load()
    {
        var query = new HeatPumpQuery
        {
            Text = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            Manufacturer = Manufacturer,
            Source = Source?.Value,
            Control = Control?.Value,
            Refrigerant = Refrigerant,
            MinPower = MinPower > 0 ? MinPower : null,
            MaxPower = MaxPower > 0 ? MaxPower : null,
            MinScop = MinScop > 0 ? MinScop : null,
            MaxOutdoorSoundPower = MaxNoise > 0 ? MaxNoise : null,
            Sort = Sort?.Value ?? HeatPumpSortOrder.Manufacturer,
            Offset = PageIndex * PageSize,
            Limit = PageSize,
        };
        var page = _catalog.SearchHeatPumps(query);
        Items.Clear();
        foreach (var hp in page.Items) Items.Add(hp);
        TotalCount = page.TotalCount;
        Selected = Items.FirstOrDefault();
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageText));
    }

    partial void OnSelectedChanged(HeatPumpModel? value)
    {
        CopPoints.Clear();
        ModelScop = 0;
        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(SelectedControl));
        if (value is null) return;

        ModelScop = En14825.ModelScop(value);
        var outdoor = value.Source == HeatSource.Air ? "A" : value.Source == HeatSource.Brine ? "B" : "W";
        foreach (var (t, flow) in new[] { (7.0, 35.0), (2.0, 35.0), (-7.0, 35.0), (7.0, 55.0), (2.0, 55.0), (-7.0, 55.0) })
        {
            var op = HeatPumpSimulator.OperatingPoint(value, t, flow);
            if (!op.IsAvailable) continue;
            var src = value.Source == HeatSource.Air ? $"{t:0}" : $"{HeatPumpSimulator.SourceTemperature(value.Source, t):0}";
            var label = value.Source == HeatSource.Air ? $"{outdoor}{src}/W{flow:0}" : $"{outdoor}{src}/W{flow:0}, зовн. {t:0} °C";
            CopPoints.Add(new CopPoint(label, op.Cop, op.MaxThermalPower / 1000));
        }
    }

    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(PageText));
    partial void OnPageIndexChanged(int value) => OnPropertyChanged(nameof(PageText));
}
