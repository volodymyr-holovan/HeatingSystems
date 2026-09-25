using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Abstractions;

public enum HeatPumpSortOrder
{
    Manufacturer,
    ScopDescending,
    PowerAscending,
    PowerDescending,
    NoiseAscending,
}

/// <summary>Filter for the heat pump catalogue.</summary>
public sealed record HeatPumpQuery
{
    public string? Text { get; init; }
    public string? Manufacturer { get; init; }
    public HeatSource? Source { get; init; }
    public CompressorControl? Control { get; init; }
    public string? Refrigerant { get; init; }
    /// <summary>Minimum / maximum rated power at 35 °C, kW.</summary>
    public double? MinPower { get; init; }
    public double? MaxPower { get; init; }
    public double? MinScop { get; init; }
    public double? MaxOutdoorSoundPower { get; init; }
    public double? MinFlowTemperature { get; init; }
    public HeatPumpSortOrder Sort { get; init; } = HeatPumpSortOrder.Manufacturer;
    public int Offset { get; init; }
    public int Limit { get; init; } = 100;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record CatalogStatistics(
    int HeatPumpCount,
    int ManufacturerCount,
    IReadOnlyDictionary<HeatSource, int> BySource,
    IReadOnlyDictionary<string, int> ByRefrigerant,
    double MedianScop,
    string DataSource);

/// <summary>Read/write access to reference data and the product catalogue.</summary>
public interface ICatalogRepository
{
    IReadOnlyList<Material> GetMaterials();
    IReadOnlyList<WindowType> GetWindowTypes();
    IReadOnlyList<ClimateLocation> GetClimateLocations();
    IReadOnlyList<EnergyCarrier> GetEnergyCarriers();
    void UpdateEnergyCarrier(string code, double pricePerUnit, double co2PerKWh);
    IReadOnlyList<HeatingTechnology> GetHeatingTechnologies();
    IReadOnlyList<EnvelopeRequirement> GetEnvelopeRequirements();

    IReadOnlyList<string> GetManufacturers();
    IReadOnlyList<string> GetRefrigerants();
    PagedResult<HeatPumpModel> SearchHeatPumps(HeatPumpQuery query);
    HeatPumpModel? GetHeatPump(int id);

    /// <summary>Candidates for a recommendation: rated power window at the reference point (W).</summary>
    IReadOnlyList<HeatPumpModel> GetHeatPumpCandidates(double minReferencePower, double maxReferencePower, IReadOnlyCollection<HeatSource> sources);

    CatalogStatistics GetStatistics();
}

/// <summary>Saved building project.</summary>
public sealed record ProjectSummary(int Id, string Name, DateTime SavedAt, string City, double DesignHeatLoad, double AnnualDemand);

public interface IProjectRepository
{
    IReadOnlyList<ProjectSummary> ListProjects();
    int SaveProject(string name, string payloadJson, string city, double designHeatLoad, double annualDemand, int? id = null);
    string? LoadProjectPayload(int id);
    void DeleteProject(int id);
}

/// <summary>Simple persistent key/value settings.</summary>
public interface ISettingsRepository
{
    string? Get(string key);
    void Set(string key, string value);
}
