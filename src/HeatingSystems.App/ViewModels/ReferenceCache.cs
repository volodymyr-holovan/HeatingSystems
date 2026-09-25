using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Models;

namespace HeatingSystems.App.ViewModels;

/// <summary>Reference data loaded once from the database and shared by the view models.</summary>
public sealed class ReferenceCache
{
    public ReferenceCache(ICatalogRepository catalog)
    {
        Climates = catalog.GetClimateLocations();
        Materials = catalog.GetMaterials();
        Windows = catalog.GetWindowTypes();
        ClimateById = Climates.ToDictionary(c => c.Id);
        MaterialById = Materials.ToDictionary(m => m.Id);
        WindowById = Windows.ToDictionary(w => w.Id);
    }

    public IReadOnlyList<ClimateLocation> Climates { get; }
    public IReadOnlyList<Material> Materials { get; }
    public IReadOnlyList<WindowType> Windows { get; }
    public IReadOnlyDictionary<int, ClimateLocation> ClimateById { get; }
    public IReadOnlyDictionary<int, Material> MaterialById { get; }
    public IReadOnlyDictionary<int, WindowType> WindowById { get; }
}
