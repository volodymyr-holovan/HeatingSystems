using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;
using HeatingSystems.Core.Projects;
using HeatingSystems.Core.Recommendations;
using HeatingSystems.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeatingSystems.Tests;

/// <summary>Creates a private copy of the shipped catalogue for each test class.</summary>
public sealed class CatalogFixture : IDisposable
{
    public CatalogFixture()
    {
        var source = Path.Combine(AppContext.BaseDirectory, DatabaseInitializer.CatalogFileName);
        if (!File.Exists(source))
            throw new FileNotFoundException("Run `python tools/build_catalog.py` before the tests.", source);
        Directory = Path.Combine(Path.GetTempPath(), "HeatingSystemsTests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        BundledPath = Path.Combine(Directory, "bundled.db");
        File.Copy(source, BundledPath);
        DatabasePath = DatabaseInitializer.EnsureUserDatabase(BundledPath, Path.Combine(Directory, "user.db"));
        Factory = new SqliteConnectionFactory(DatabasePath);
        Catalog = new SqliteCatalogRepository(Factory);
        Projects = new SqliteProjectRepository(Factory);
    }

    public string Directory { get; }
    public string BundledPath { get; }
    public string DatabasePath { get; }
    public SqliteConnectionFactory Factory { get; }
    public SqliteCatalogRepository Catalog { get; }
    public SqliteProjectRepository Projects { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch (IOException) { }
    }
}

public class CatalogRepositoryTests(CatalogFixture db) : IClassFixture<CatalogFixture>
{
    [Fact]
    public void Catalogue_ContainsAtLeast5000RealHeatPumps()
    {
        var stats = db.Catalog.GetStatistics();
        Assert.True(stats.HeatPumpCount >= 5000, $"Only {stats.HeatPumpCount} heat pumps");
        Assert.True(stats.ManufacturerCount >= 100);
        Assert.InRange(stats.MedianScop, 3.5, 6.0);
        Assert.Contains("KEYMARK", stats.DataSource);
        Assert.Equal(stats.HeatPumpCount, stats.BySource.Values.Sum());
    }

    [Fact]
    public void ReferenceData_IsComplete()
    {
        Assert.True(db.Catalog.GetMaterials().Count >= 20);
        Assert.True(db.Catalog.GetWindowTypes().Count >= 5);
        Assert.Equal(25, db.Catalog.GetClimateLocations().Count);
        var carriers = db.Catalog.GetEnergyCarriers().Select(c => c.Code).ToHashSet();
        Assert.All(db.Catalog.GetHeatingTechnologies(), t => Assert.Contains(t.CarrierCode, carriers));
        Assert.Equal(12, db.Catalog.GetEnvelopeRequirements().Count);
    }

    [Fact]
    public void PerformanceModel_ReproducesCertifiedScop()
    {
        // Validation of the whole performance model: the EN 14825 average-climate SCOP computed from the regression
        // coefficients must match the certified SCOP of each product (median error < 6 %, 90 % of products < 12 %).
        var all = db.Catalog.SearchHeatPumps(new HeatPumpQuery { Limit = 10_000 }).Items;
        Assert.Equal(db.Catalog.GetStatistics().HeatPumpCount, all.Count);
        var errors = all.Select(hp => Math.Abs(En14825.ModelScop(hp) - hp.Scop) / hp.Scop).OrderBy(e => e).ToList();
        var median = errors[errors.Count / 2];
        var p90 = errors[(int)(errors.Count * 0.9)];
        Assert.True(median < 0.06, $"Median SCOP error {median:P1}");
        Assert.True(p90 < 0.12, $"90th percentile SCOP error {p90:P1}");
    }

    [Fact]
    public void Search_FiltersAndPages()
    {
        var q = new HeatPumpQuery { Source = HeatSource.Brine, MinPower = 5, MaxPower = 12, Sort = HeatPumpSortOrder.ScopDescending, Limit = 10 };
        var page = db.Catalog.SearchHeatPumps(q);
        Assert.True(page.TotalCount > 10);
        Assert.Equal(10, page.Items.Count);
        Assert.All(page.Items, hp =>
        {
            Assert.Equal(HeatSource.Brine, hp.Source);
            Assert.InRange(hp.RatedPowerLowTemp, 5, 12);
        });
        Assert.True(page.Items.Zip(page.Items.Skip(1)).All(p => p.First.Scop >= p.Second.Scop));

        var next = db.Catalog.SearchHeatPumps(q with { Offset = 10 });
        Assert.Empty(page.Items.Select(i => i.Id).Intersect(next.Items.Select(i => i.Id)));
    }

    [Fact]
    public void Search_ByTextMatchesAllWords_AndEscapesWildcards()
    {
        var man = db.Catalog.GetManufacturers().First(m => m.Contains("Vaillant", StringComparison.OrdinalIgnoreCase));
        var result = db.Catalog.SearchHeatPumps(new HeatPumpQuery { Text = "vaillant VWL" });
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, hp => Assert.Equal(man, hp.Manufacturer));
        Assert.Equal(0, db.Catalog.SearchHeatPumps(new HeatPumpQuery { Text = "%_%zzz" }).TotalCount);
    }

    [Fact]
    public void Candidates_RespectPowerWindow()
    {
        var c = db.Catalog.GetHeatPumpCandidates(5000, 8000, new[] { HeatSource.Air });
        Assert.NotEmpty(c);
        Assert.All(c, hp => Assert.InRange(hp.ThermalPowerRef, 5000, 8000));
        Assert.Equal(c[0].Id, db.Catalog.GetHeatPump(c[0].Id)!.Id);
    }
}

public class ProjectRepositoryTests(CatalogFixture db) : IClassFixture<CatalogFixture>
{
    [Fact]
    public void Project_RoundTripsThroughDatabase()
    {
        var climates = db.Catalog.GetClimateLocations().ToDictionary(c => c.Id);
        var materials = db.Catalog.GetMaterials().ToDictionary(m => m.Id);
        var windows = db.Catalog.GetWindowTypes().ToDictionary(w => w.Id);

        var building = TestData.House(climates[1]);
        var json = ProjectData.FromBuilding(building).ToJson();
        var id = db.Projects.SaveProject("House", json, building.Climate.City, 7000, 15000);

        var restored = ProjectData.FromJson(db.Projects.LoadProjectPayload(id)!).ToBuilding(climates, materials, windows);
        Assert.Equal(building.HeatedFloorArea, restored.HeatedFloorArea);
        Assert.Equal(building.Wall.Layers.Count, restored.Wall.Layers.Count);
        Assert.Equal(building.Emitter, restored.Emitter);
        Assert.Contains(db.Projects.ListProjects(), p => p.Id == id && p.Name == "House");

        db.Projects.SaveProject("House 2", json, "Kyiv", 1, 2, id);
        Assert.Contains(db.Projects.ListProjects(), p => p.Id == id && p.Name == "House 2");

        db.Projects.DeleteProject(id);
        Assert.Null(db.Projects.LoadProjectPayload(id));
    }

    [Fact]
    public void UpdateEnergyCarrier_PersistsAndValidates()
    {
        db.Catalog.UpdateEnergyCarrier("electricity", 5.5, 0.3);
        var el = db.Catalog.GetEnergyCarriers().Single(c => c.Code == "electricity");
        Assert.Equal(5.5, el.PricePerUnit);
        Assert.Throws<ArgumentOutOfRangeException>(() => db.Catalog.UpdateEnergyCarrier("electricity", -1, 0));
        Assert.Throws<KeyNotFoundException>(() => db.Catalog.UpdateEnergyCarrier("unknown", 1, 0));
    }

    [Fact]
    public void NewCatalogueVersion_KeepsUserProjectsAndTariffs()
    {
        var userDb = Path.Combine(db.Directory, "migrate-user.db");
        DatabaseInitializer.EnsureUserDatabase(db.BundledPath, userDb);
        var factory = new SqliteConnectionFactory(userDb);
        var id = new SqliteProjectRepository(factory).SaveProject("My project", "{}", "Kyiv", 1, 2);
        new SqliteCatalogRepository(factory).UpdateEnergyCarrier("natural_gas", 12.34, 0.2);

        // Ship a "newer" catalogue.
        var newer = Path.Combine(db.Directory, "newer.db");
        File.Copy(db.BundledPath, newer);
        using (var c = new SqliteConnection($"Data Source={newer};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE meta SET value = 'test-next' WHERE key = 'catalog_version'";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        DatabaseInitializer.EnsureUserDatabase(newer, userDb);
        Assert.Equal("test-next", DatabaseInitializer.ReadMeta(userDb, "catalog_version"));
        var migrated = new SqliteConnectionFactory(userDb);
        Assert.Equal("{}", new SqliteProjectRepository(migrated).LoadProjectPayload(id));
        Assert.Equal(12.34, new SqliteCatalogRepository(migrated).GetEnergyCarriers().Single(c => c.Code == "natural_gas").PricePerUnit);
        Assert.True(File.Exists(userDb + ".bak"));
    }

    [Fact]
    public void MissingCatalogue_CreatesEmptySchema()
    {
        var path = Path.Combine(db.Directory, "empty.db");
        DatabaseInitializer.EnsureUserDatabase(Path.Combine(db.Directory, "does-not-exist.db"), path);
        var repo = new SqliteCatalogRepository(new SqliteConnectionFactory(path));
        Assert.Equal(0, repo.GetStatistics().HeatPumpCount);
    }
}

public class RecommendationTests(CatalogFixture db) : IClassFixture<CatalogFixture>
{
    [Fact]
    public void Recommender_ReturnsSizedUnitsOrderedByCost()
    {
        var climate = db.Catalog.GetClimateLocations().Single(c => c.CityEn == "Kyiv");
        var b = TestData.House(climate, EmitterType.Underfloor);
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);
        var electricity = db.Catalog.GetEnergyCarriers().Single(c => c.Code == "electricity");
        var options = new RecommendationOptions { Count = 10 };

        var list = new HeatPumpRecommender(db.Catalog).Recommend(b, loss, demand, electricity, options);

        Assert.Equal(10, list.Count);
        Assert.Equal(Enumerable.Range(1, 10), list.Select(r => r.Rank));
        Assert.True(list.Zip(list.Skip(1)).All(p => p.First.AnnualCost <= p.Second.AnnualCost));
        Assert.All(list, r =>
        {
            Assert.True(r.Result.Coverage >= options.MinCoverage);
            Assert.True(r.Result.HeatPump.RatedPowerLowTemp * 1000 <= options.MaxSizingRatio * loss.DesignHeatLoad);
            Assert.InRange(r.Result.OverallSpf, 1.5, 7.0);
        });
        Assert.Equal(list.Count, list.Select(r => (r.Result.HeatPump.Manufacturer, r.Result.HeatPump.Model)).Distinct().Count());
        // Identical certified units must be merged into one entry (variants), not occupy several ranks.
        Assert.Equal(list.Count, list.Select(r => (r.Result.HeatPump.Manufacturer, r.Result.HeatPump.Scop,
            Math.Round(r.Result.HeatPump.ElectricPowerRef), Math.Round(r.Result.HeatPump.CopP3, 6))).Distinct().Count());
    }

    [Fact]
    public void Recommender_HonoursConstraints()
    {
        var climate = db.Catalog.GetClimateLocations().Single(c => c.CityEn == "Lviv");
        var b = TestData.House(climate);
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);
        var electricity = db.Catalog.GetEnergyCarriers().Single(c => c.Code == "electricity");
        var options = new RecommendationOptions { Sources = new[] { HeatSource.Air }, NaturalRefrigerantsOnly = true, MaxOutdoorSoundPower = 60 };

        var list = new HeatPumpRecommender(db.Catalog).Recommend(b, loss, demand, electricity, options);

        Assert.NotEmpty(list);
        Assert.All(list, r =>
        {
            Assert.Equal(HeatSource.Air, r.Result.HeatPump.Source);
            Assert.Equal("R290", r.Result.HeatPump.Refrigerant);
            Assert.True(r.Result.HeatPump.SoundPowerOutdoor <= 60);
            Assert.True(r.Result.HeatPump.MaxFlowTemperature >= 55);
        });
    }

    [Fact]
    public void EnvelopeCheck_UsesZoneRequirements()
    {
        var climate = db.Catalog.GetClimateLocations().Single(c => c.CityEn == "Kyiv");
        var items = EnvelopeCheck.Check(TestData.House(climate), db.Catalog.GetEnvelopeRequirements());
        var wall = items.Single(i => i.Element == EnvelopeElement.Wall);
        Assert.Equal(3.3, wall.RequiredResistance);
        Assert.False(wall.Complies); // R = 3.16 < 3.3
        Assert.True(items.Single(i => i.Element == EnvelopeElement.Window).Complies); // 1/1.3 = 0.77 ≥ 0.75
    }
}
