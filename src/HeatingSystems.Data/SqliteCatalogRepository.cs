using System.Globalization;
using System.Text;
using HeatingSystems.Core.Abstractions;
using HeatingSystems.Core.Models;
using Microsoft.Data.Sqlite;

namespace HeatingSystems.Data;

/// <summary>SQLite implementation of the catalogue and reference data repository.</summary>
public sealed class SqliteCatalogRepository(SqliteConnectionFactory factory) : ICatalogRepository
{
    private const string HeatPumpColumns = """
        id, manufacturer, series, model, source_type, control, certification_date, refrigerant, refrigerant_mass_kg,
        rated_power_low, rated_power_medium, scop, eta_s_low, eta_s_medium, sound_power_indoor, sound_power_outdoor,
        max_flow_temperature, bivalent_temperature, operation_limit_temperature, p_th_ref, p_el_ref,
        cop_p1, cop_p2, cop_p3, cop_p4, pel_p1, pel_p2, pel_p3, pel_p4
        """;

    public IReadOnlyList<Material> GetMaterials() => Query(
        "SELECT id, name_en, name_uk, category, conductivity, density, source FROM materials ORDER BY category, id",
        r => new Material(r.GetInt32(0), r.GetString(1), r.GetString(2), (MaterialCategory)r.GetInt32(3), r.GetDouble(4), r.GetDouble(5), r.GetString(6)));

    public IReadOnlyList<WindowType> GetWindowTypes() => Query(
        "SELECT id, name_en, name_uk, u_value, g_value, source FROM window_types ORDER BY u_value DESC",
        r => new WindowType(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDouble(3), r.GetDouble(4), r.GetString(5)));

    public IReadOnlyList<ClimateLocation> GetClimateLocations() => Query(
        """
        SELECT id, city_en, city_uk, region_en, region_uk, design_temperature, heating_days, heating_mean_temperature,
               annual_mean_temperature, source
        FROM climate_locations ORDER BY city_en
        """,
        r => new ClimateLocation(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDouble(5),
            r.GetInt32(6), r.GetDouble(7), r.GetDouble(8), r.GetString(9)));

    public IReadOnlyList<EnergyCarrier> GetEnergyCarriers() => Query(
        "SELECT code, name_en, name_uk, unit_en, unit_uk, energy_per_unit, price_per_unit, co2_per_kwh, source FROM energy_carriers ORDER BY rowid",
        r => new EnergyCarrier(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDouble(5),
            r.GetDouble(6), r.GetDouble(7), r.GetString(8)));

    public void UpdateEnergyCarrier(string code, double pricePerUnit, double co2PerKWh)
    {
        if (pricePerUnit < 0) throw new ArgumentOutOfRangeException(nameof(pricePerUnit));
        if (co2PerKWh < 0) throw new ArgumentOutOfRangeException(nameof(co2PerKWh));
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE energy_carriers SET price_per_unit = $p, co2_per_kwh = $c, updated_at = $u WHERE code = $code";
        cmd.Parameters.AddWithValue("$p", pricePerUnit);
        cmd.Parameters.AddWithValue("$c", co2PerKWh);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$code", code);
        if (cmd.ExecuteNonQuery() == 0) throw new KeyNotFoundException($"Energy carrier '{code}' not found.");
    }

    public IReadOnlyList<HeatingTechnology> GetHeatingTechnologies() => Query(
        """
        SELECT code, name_en, name_uk, carrier_code, seasonal_efficiency, low_temperature_bonus, description_en, description_uk, source
        FROM heating_technologies ORDER BY rowid
        """,
        r => new HeatingTechnology(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDouble(4), r.GetDouble(5),
            r.GetString(6), r.GetString(7), r.GetString(8)));

    public IReadOnlyList<EnvelopeRequirement> GetEnvelopeRequirements() => Query(
        "SELECT climate_zone, element, min_resistance, source FROM envelope_requirements",
        r => new EnvelopeRequirement(r.GetInt32(0), (EnvelopeElement)r.GetInt32(1), r.GetDouble(2), r.GetString(3)));

    public IReadOnlyList<string> GetManufacturers() => Query(
        "SELECT name FROM manufacturers ORDER BY name COLLATE NOCASE", r => r.GetString(0));

    public IReadOnlyList<string> GetRefrigerants() => Query(
        "SELECT refrigerant FROM heat_pumps GROUP BY refrigerant ORDER BY COUNT(*) DESC", r => r.GetString(0));

    public PagedResult<HeatPumpModel> SearchHeatPumps(HeatPumpQuery q)
    {
        var where = new StringBuilder("WHERE 1 = 1");
        var parameters = new List<SqliteParameter>();
        void Add(string clause, string name, object value)
        {
            where.Append(" AND ").Append(clause);
            parameters.Add(new SqliteParameter(name, value));
        }

        if (!string.IsNullOrWhiteSpace(q.Text))
        {
            // Every word must appear in manufacturer, series or model (case-insensitive for ASCII).
            var words = q.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < words.Length && i < 8; i++)
                Add($"(manufacturer LIKE $w{i} ESCAPE '\\' OR model LIKE $w{i} ESCAPE '\\' OR series LIKE $w{i} ESCAPE '\\')",
                    $"$w{i}", "%" + EscapeLike(words[i]) + "%");
        }
        if (!string.IsNullOrEmpty(q.Manufacturer)) Add("manufacturer = $man", "$man", q.Manufacturer);
        if (q.Source is { } s) Add("source_type = $src", "$src", (int)s);
        if (q.Control is { } ctl) Add("control = $ctl", "$ctl", (int)ctl);
        if (!string.IsNullOrEmpty(q.Refrigerant)) Add("refrigerant = $ref", "$ref", q.Refrigerant);
        if (q.MinPower is { } minP) Add("rated_power_low >= $minP", "$minP", minP);
        if (q.MaxPower is { } maxP) Add("rated_power_low <= $maxP", "$maxP", maxP);
        if (q.MinScop is { } minS) Add("scop >= $minS", "$minS", minS);
        if (q.MaxOutdoorSoundPower is { } noise) Add("sound_power_outdoor <= $noise", "$noise", noise);
        if (q.MinFlowTemperature is { } flow) Add("max_flow_temperature >= $flow", "$flow", flow);

        var order = q.Sort switch
        {
            HeatPumpSortOrder.ScopDescending => "scop DESC, manufacturer COLLATE NOCASE, model COLLATE NOCASE",
            HeatPumpSortOrder.PowerAscending => "rated_power_low, manufacturer COLLATE NOCASE",
            HeatPumpSortOrder.PowerDescending => "rated_power_low DESC, manufacturer COLLATE NOCASE",
            HeatPumpSortOrder.NoiseAscending => "sound_power_outdoor IS NULL, sound_power_outdoor, scop DESC",
            _ => "manufacturer COLLATE NOCASE, model COLLATE NOCASE, rated_power_low",
        };

        using var c = factory.Open();
        using var count = c.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM v_heat_pumps {where}";
        foreach (var p in parameters) count.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {HeatPumpColumns} FROM v_heat_pumps {where} ORDER BY {order} LIMIT $limit OFFSET $offset";
        foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(q.Limit, 1, 10_000));
        cmd.Parameters.AddWithValue("$offset", Math.Max(0, q.Offset));
        return new PagedResult<HeatPumpModel>(Read(cmd, MapHeatPump), total);
    }

    public HeatPumpModel? GetHeatPump(int id)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {HeatPumpColumns} FROM v_heat_pumps WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return Read(cmd, MapHeatPump).FirstOrDefault();
    }

    public IReadOnlyList<HeatPumpModel> GetHeatPumpCandidates(double minReferencePower, double maxReferencePower, IReadOnlyCollection<HeatSource> sources)
    {
        if (sources.Count == 0) return Array.Empty<HeatPumpModel>();
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        var names = sources.Select((s, i) => $"$s{i}").ToList();
        cmd.CommandText = $"""
            SELECT {HeatPumpColumns} FROM v_heat_pumps
            WHERE source_type IN ({string.Join(", ", names)}) AND p_th_ref BETWEEN $min AND $max
            """;
        var index = 0;
        foreach (var s in sources) cmd.Parameters.AddWithValue(names[index++], (int)s);
        cmd.Parameters.AddWithValue("$min", minReferencePower);
        cmd.Parameters.AddWithValue("$max", maxReferencePower);
        return Read(cmd, MapHeatPump);
    }

    public CatalogStatistics GetStatistics()
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), (SELECT COUNT(*) FROM manufacturers) FROM heat_pumps";
        int count, manufacturers;
        using (var r = cmd.ExecuteReader())
        {
            r.Read();
            count = r.GetInt32(0);
            manufacturers = r.GetInt32(1);
        }

        cmd.CommandText = "SELECT source_type, COUNT(*) FROM heat_pumps GROUP BY source_type";
        var bySource = Read(cmd, r => (Source: (HeatSource)r.GetInt32(0), Count: r.GetInt32(1))).ToDictionary(x => x.Source, x => x.Count);

        cmd.CommandText = "SELECT refrigerant, COUNT(*) FROM heat_pumps GROUP BY refrigerant ORDER BY 2 DESC";
        var byRefrigerant = Read(cmd, r => (Name: r.GetString(0), Count: r.GetInt32(1))).ToDictionary(x => x.Name, x => x.Count);

        cmd.CommandText = "SELECT scop FROM heat_pumps ORDER BY scop LIMIT 1 OFFSET (SELECT COUNT(*) FROM heat_pumps) / 2";
        var median = count > 0 ? Convert.ToDouble(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) : 0;

        cmd.CommandText = "SELECT value FROM meta WHERE key = 'data_source'";
        var source = cmd.ExecuteScalar() as string ?? "";
        return new CatalogStatistics(count, manufacturers, bySource, byRefrigerant, median, source);
    }

    private static HeatPumpModel MapHeatPump(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Manufacturer = r.GetString(1),
        Series = r.GetString(2),
        Model = r.GetString(3),
        Source = (HeatSource)r.GetInt32(4),
        Control = (CompressorControl)r.GetInt32(5),
        CertificationDate = r.IsDBNull(6) ? null : DateOnly.ParseExact(r.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Refrigerant = r.GetString(7),
        RefrigerantMassKg = NullableDouble(r, 8),
        RatedPowerLowTemp = r.GetDouble(9),
        RatedPowerMediumTemp = r.GetDouble(10),
        Scop = r.GetDouble(11),
        EtaSLowTemp = r.GetDouble(12),
        EtaSMediumTemp = r.GetDouble(13),
        SoundPowerIndoor = NullableDouble(r, 14),
        SoundPowerOutdoor = NullableDouble(r, 15),
        MaxFlowTemperature = r.GetDouble(16),
        BivalentTemperature = NullableDouble(r, 17),
        OperationLimitTemperature = NullableDouble(r, 18),
        ThermalPowerRef = r.GetDouble(19),
        ElectricPowerRef = r.GetDouble(20),
        CopP1 = r.GetDouble(21),
        CopP2 = r.GetDouble(22),
        CopP3 = r.GetDouble(23),
        CopP4 = r.GetDouble(24),
        PelP1 = r.GetDouble(25),
        PelP2 = r.GetDouble(26),
        PelP3 = r.GetDouble(27),
        PelP4 = r.GetDouble(28),
    };

    private static double? NullableDouble(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDouble(i);

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Read(cmd, map);
    }

    private static List<T> Read<T>(SqliteCommand cmd, Func<SqliteDataReader, T> map)
    {
        var list = new List<T>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(map(r));
        return list;
    }
}
